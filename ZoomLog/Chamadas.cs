using System.Diagnostics;
using System.Text;

namespace ZoomLog.Cliente;

/// Um evento por chamada que a aplicação faz a outro serviço — a NFS-e de São
/// Paulo, o MNI de um tribunal, o DataJud, outra aplicação da casa.
///
/// Guarda sempre o JSON do pedido e o da resposta (tapado e cortado).
///
/// Entra em todos os `HttpClient` da fábrica (`ConfigureHttpClientDefaults`).
/// Um `new HttpClient()` feito à mão passa ao lado: esses têm de receber o
/// handler explicitamente (`ZoomLog.Handler(servicos)`).
public sealed class ChamadasZoomLog(FilaZoomLog fila, Mascara mascara) : DelegatingHandler
{
    private readonly ZoomLogOpcoes o = fila.Opcoes;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
    {
        var host = pedido.RequestUri?.Host ?? "";
        if (!o.Ativo || Casa(host, o.SaidaIgnorada) || EOProprioZoomLog(pedido.RequestUri))
            return await base.SendAsync(pedido, ct);

        // O corpo do pedido lê-se antes de sair: depois de enviado, um
        // StreamContent já foi consumido e não há volta.
        string? corpoPedido = null;
        var truncado = false;
        if (pedido.Content is { } c && Mascara.ETexto(c.Headers.ContentType?.MediaType)
            && c.Headers.ContentLength is null or <= 1024 * 1024)
        {
            await c.LoadIntoBufferAsync(ct);
            corpoPedido = await Ler(c, o.CorpoMaximo, ct);
        }

        var pai = Activity.Current;
        var inicio = Stopwatch.GetTimestamp();
        HttpResponseMessage? resposta = null;
        Exception? falha = null;

        try
        {
            resposta = await base.SendAsync(pedido, ct);
            return resposta;
        }
        catch (Exception e)
        {
            falha = e;
            throw;
        }
        finally
        {
            try
            {
                var duracao = (int)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
                var estado = resposta is null ? (int?)null : (int)resposta.StatusCode;

                var evento = new EventoZoomLog
                {
                    Tipo = TipoEvento.Chamada,
                    Nivel = falha is not null || estado >= 500 ? NivelEvento.Erro
                        : estado >= 400 ? NivelEvento.Aviso
                        : NivelEvento.Info,
                    Categoria = "Chamada",
                    Metodo = pedido.Method.Method,
                    Destino = host,
                    Caminho = pedido.RequestUri?.AbsolutePath + mascara.Texto(pedido.RequestUri?.Query),
                    Estado = estado,
                    DuracaoMs = duracao,
                    TraceId = pai?.TraceId.ToHexString(),
                    PaiId = pai?.SpanId.ToHexString(),
                    // O traceparent que o .NET pôs no pedido é o que a aplicação
                    // do outro lado vai ver como pai. Usá-lo como identificador
                    // deste passo é o que cose as duas pontas no ecrã.
                    SpanId = SpanDoTraceparent(pedido) ?? ActivitySpanId.CreateRandom().ToHexString(),
                };

                evento.TraceId ??= TraceDoTraceparent(pedido);

                evento.Mensagem = falha is not null
                    ? $"{evento.Metodo} {host}{pedido.RequestUri?.AbsolutePath} falhou em {duracao} ms: {falha.Message}"
                    : $"{evento.Metodo} {host}{pedido.RequestUri?.AbsolutePath} → {estado} em {duracao} ms";

                if (falha is not null)
                {
                    evento.ExcecaoTipo = falha.GetType().FullName;
                    evento.Excecao = falha.ToString();
                }

                // O JSON do pedido e da resposta vai sempre.
                {
                    var corpo = new CorpoZoomLog
                    {
                        PedidoCabecalhos = mascara.Cabecalhos(pedido.Headers.Concat(
                            pedido.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())),
                        Pedido = mascara.Texto(Mascara.Cortar(corpoPedido, o.CorpoMaximo, ref truncado)),
                        TipoPedido = pedido.Content?.Headers.ContentType?.ToString(),
                        PedidoTamanho = pedido.Content?.Headers.ContentLength,
                    };

                    if (resposta is not null)
                    {
                        corpo.RespostaCabecalhos = mascara.Cabecalhos(resposta.Headers.Concat(resposta.Content.Headers));
                        corpo.TipoResposta = resposta.Content.Headers.ContentType?.ToString();
                        corpo.RespostaTamanho = resposta.Content.Headers.ContentLength;

                        // Carregar em memória é o que quem chamou vai fazer a
                        // seguir de qualquer maneira; a resposta fica pronta
                        // para ele ler, sem voltar à rede. Um ficheiro grande
                        // ou binário fica de fora.
                        if (Mascara.ETexto(corpo.TipoResposta) && corpo.RespostaTamanho is null or <= 5 * 1024 * 1024)
                        {
                            await resposta.Content.LoadIntoBufferAsync(ct);
                            var texto = await Ler(resposta.Content, o.CorpoMaximo, ct);
                            if (texto is not null && texto.Length >= o.CorpoMaximo) truncado = true;
                            corpo.Resposta = mascara.Texto(texto);
                        }
                    }

                    corpo.Truncado = truncado;
                    evento.Corpo = corpo;
                }

                fila.Escrever(evento);
            }
            catch
            {
                // O registo nunca pode ser o motivo de uma chamada falhar.
            }
        }
    }

    /// Lê só o princípio de um conteúdo já em memória, sem o estragar para
    /// quem vem a seguir.
    ///
    /// O `ReadAsStreamAsync` de um conteúdo carregado devolve **sempre o mesmo
    /// fluxo** — o .NET guarda-o. Fechá-lo (um `using`) ou deixá-lo no fim
    /// partia a leitura de quem chamou: foi o `ObjectDisposedException: Cannot
    /// access a closed Stream` no `ReadFromJsonAsync` da sincronização com o
    /// ZoomTicket. Por isso: não se fecha, e volta à posição onde estava.
    private static async Task<string?> Ler(HttpContent conteudo, int maximo, CancellationToken ct)
    {
        var fluxo = await conteudo.ReadAsStreamAsync(ct);
        if (!fluxo.CanSeek) return null; // não carregado: ler aqui era roubá-lo a quem chamou

        var inicio = fluxo.Position;
        try
        {
            using var leitor = new StreamReader(fluxo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096, leaveOpen: true);
            var buffer = new char[maximo];
            var lidos = await leitor.ReadBlockAsync(buffer.AsMemory(), ct);
            return lidos == 0 ? null : new string(buffer, 0, lidos);
        }
        finally
        {
            fluxo.Position = inicio;
        }
    }

    private static string? SpanDoTraceparent(HttpRequestMessage pedido) =>
        Traceparent(pedido) is { Length: 55 } t ? t.Substring(36, 16) : null;

    private static string? TraceDoTraceparent(HttpRequestMessage pedido) =>
        Traceparent(pedido) is { Length: 55 } t ? t.Substring(3, 32) : null;

    private static string? Traceparent(HttpRequestMessage pedido) =>
        pedido.Headers.TryGetValues("traceparent", out var v) ? v.FirstOrDefault() : null;

    private bool EOProprioZoomLog(Uri? uri) =>
        uri is not null && o.Endereco.Length > 0
        && uri.AbsoluteUri.StartsWith(o.Endereco.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    /// `*.jus.br` casa com `pje.tjsp.jus.br` e com `jus.br`; um nome sem
    /// asterisco só consigo mesmo.
    public static bool Casa(string host, IEnumerable<string> padroes)
    {
        foreach (var p in padroes)
        {
            if (p.StartsWith("*."))
            {
                var dominio = p[2..];
                if (host.Equals(dominio, StringComparison.OrdinalIgnoreCase)
                    || host.EndsWith("." + dominio, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (host.Equals(p, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
