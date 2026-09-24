using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ZoomLog.Cliente;

/// A fila entre a aplicação e o ZoomLog.
///
/// Quem regista nunca espera: escreve na fila e segue. A fila tem tamanho
/// fixo, e cheia deita fora o evento mais antigo em vez de crescer — um
/// ZoomLog em baixo não pode ser o que deita abaixo a aplicação por falta de
/// memória. Os que se perdem contam-se, e a conta vai no envio seguinte.
public sealed class FilaZoomLog
{
    private readonly Channel<EventoZoomLog> canal;
    private long perdidos;

    public FilaZoomLog(IOptions<ZoomLogOpcoes> opcoes)
    {
        Opcoes = opcoes.Value;
        canal = Channel.CreateBounded<EventoZoomLog>(
            new BoundedChannelOptions(Math.Max(100, Opcoes.Capacidade))
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            },
            _ => Interlocked.Increment(ref perdidos));
    }

    public ZoomLogOpcoes Opcoes { get; }

    public void Escrever(EventoZoomLog evento)
    {
        if (!Opcoes.Ativo) return;
        evento.Ambiente ??= Opcoes.Ambiente;
        evento.Maquina ??= Identidade.Maquina;
        evento.Versao ??= Identidade.Versao;
        canal.Writer.TryWrite(evento);
    }

    internal ChannelReader<EventoZoomLog> Leitor => canal.Reader;
    internal long TirarPerdidos() => Interlocked.Exchange(ref perdidos, 0);
    internal void Fechar() => canal.Writer.TryComplete();
}

/// Envia em lotes: até `Lote` eventos ou `IntervaloMs`, o que chegar primeiro.
///
/// Um lote que não entra fica na mão e é tentado outra vez, com esperas a
/// dobrar até um minuto. O servidor só responde 200 depois de gravar — um 200
/// daqui quer dizer que está na base, e não apenas que a ligação correu.
public sealed class EnviadorZoomLog(FilaZoomLog fila, ConsolaZoomLog consola) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient http = CriarCliente(fila.Opcoes);

    /// Um cliente só dele, fora da fábrica do `IHttpClientFactory`: se passasse
    /// pelo registo de chamadas de saída, cada envio de logs gerava um log de
    /// chamada, que gerava um envio, e assim por diante.
    private static HttpClient CriarCliente(ZoomLogOpcoes opcoes)
    {
        var http = new HttpClient(new SocketsHttpHandler
        {
            // Sem traceparent: o envio de logs não é passo de nenhum pedido.
            ActivityHeadersPropagator = null,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(15),
        };

        if (opcoes.Ativo)
        {
            http.BaseAddress = new Uri(opcoes.Endereco.TrimEnd('/') + "/");
            http.DefaultRequestHeaders.Add("X-ZoomLog-Chave", opcoes.Chave);
        }

        return http;
    }

    protected override async Task ExecuteAsync(CancellationToken paragem)
    {
        if (!fila.Opcoes.Ativo) return;

        // Fora de qualquer pedido: o que se faz aqui não pertence a trace nenhum.
        Activity.Current = null;

        var lote = new List<EventoZoomLog>(fila.Opcoes.Lote);
        var espera = TimeSpan.FromSeconds(1);

        while (!paragem.IsCancellationRequested)
        {
            try
            {
                if (lote.Count == 0)
                    await Encher(lote, paragem);

                if (lote.Count == 0) continue;

                var perdidos = fila.TirarPerdidos();
                if (perdidos > 0)
                    lote.Add(new EventoZoomLog
                    {
                        Nivel = NivelEvento.Aviso,
                        Categoria = "ZoomLog.Cliente",
                        Mensagem = $"{perdidos} evento(s) perdido(s): a fila encheu enquanto o ZoomLog não respondia.",
                        Ambiente = fila.Opcoes.Ambiente,
                        Maquina = Identidade.Maquina,
                        Versao = Identidade.Versao,
                    });

                await Enviar(lote, paragem);
                lote.Clear();
                espera = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (paragem.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                consola.Aviso($"ZoomLog: envio de {lote.Count} evento(s) falhou ({e.Message}); nova tentativa em {espera.TotalSeconds:0} s.");
                try { await Task.Delay(espera, paragem); } catch (OperationCanceledException) { break; }
                espera = TimeSpan.FromSeconds(Math.Min(60, espera.TotalSeconds * 2));

                // Um lote que ficou na mão enquanto a fila continua a encher:
                // os mais antigos cedem o lugar, como na própria fila.
                if (lote.Count > fila.Opcoes.Capacidade) lote.RemoveRange(0, lote.Count - fila.Opcoes.Capacidade);
            }
        }
    }

    private async Task Encher(List<EventoZoomLog> lote, CancellationToken paragem)
    {
        var leitor = fila.Leitor;
        if (!await leitor.WaitToReadAsync(paragem)) return;

        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(paragem);
        prazo.CancelAfter(fila.Opcoes.IntervaloMs);

        try
        {
            while (lote.Count < fila.Opcoes.Lote)
            {
                while (lote.Count < fila.Opcoes.Lote && leitor.TryRead(out var e)) lote.Add(e);
                if (lote.Count >= fila.Opcoes.Lote) break;
                if (!await leitor.WaitToReadAsync(prazo.Token)) break;
            }
        }
        catch (OperationCanceledException) when (!paragem.IsCancellationRequested)
        {
            // Passou o intervalo: vai o que houver.
        }
    }

    private async Task Enviar(List<EventoZoomLog> lote, CancellationToken ct)
    {
        using var corpo = new MemoryStream();
        await using (var gz = new GZipStream(corpo, CompressionLevel.Fastest, leaveOpen: true))
            await JsonSerializer.SerializeAsync(gz, new LoteZoomLog { Eventos = lote }, Json, ct);

        corpo.Position = 0;
        using var conteudo = new StreamContent(corpo);
        conteudo.Headers.ContentType = new("application/json");
        conteudo.Headers.ContentEncoding.Add("gzip");

        using var resposta = await http.PostAsync("api/ingestao", conteudo, ct);

        // 4xx de validação não se repete: tentar outra vez dava o mesmo. Uma
        // chave recusada também não — mas avisa-se, porque é configuração.
        if ((int)resposta.StatusCode is >= 400 and < 500 && resposta.StatusCode != HttpStatusCode.TooManyRequests)
        {
            consola.Aviso($"ZoomLog recusou {lote.Count} evento(s): {(int)resposta.StatusCode} {await resposta.Content.ReadAsStringAsync(ct)}");
            return;
        }

        resposta.EnsureSuccessStatusCode();
    }

    /// Ao parar a aplicação, o que está na fila ainda tem alguns segundos para sair.
    public override async Task StopAsync(CancellationToken ct)
    {
        await base.StopAsync(ct);
        await EsvaziarAsync();
    }

    /// Envia já tudo o que está na fila e fecha-a. É o que um processo que
    /// não chega a `app.Run()` — um CronJob que corre e sai — tem de chamar no
    /// fim: sem `Run`, este serviço nunca arrancou e a fila ficava por enviar.
    /// Ver `ZoomLogExtensoes.EnviarZoomLogAsync`.
    public async Task EsvaziarAsync()
    {
        if (!fila.Opcoes.Ativo) return;

        fila.Fechar();
        var resto = new List<EventoZoomLog>();
        while (fila.Leitor.TryRead(out var e)) resto.Add(e);
        if (resto.Count == 0) return;

        using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            foreach (var parte in resto.Chunk(fila.Opcoes.Lote))
                await Enviar([.. parte], prazo.Token);
        }
        catch (Exception e)
        {
            consola.Aviso($"ZoomLog: {resto.Count} evento(s) ficaram por enviar ao parar ({e.Message}).");
        }
    }
}

/// Escreve na consola sem passar pelo `ILogger` — um aviso sobre o envio de
/// logs que fosse para a fila de logs dava voltas sem sair do sítio.
public sealed class ConsolaZoomLog
{
    private DateTime ultimo = DateTime.MinValue;

    public void Aviso(string texto)
    {
        // Um aviso por minuto chega para quem lê o `kubectl logs`.
        if (DateTime.UtcNow - ultimo < TimeSpan.FromMinutes(1)) return;
        ultimo = DateTime.UtcNow;
        Console.Error.WriteLine($"warn: {texto}");
    }
}

internal static class Identidade
{
    public static readonly string Maquina = Environment.MachineName;

    public static readonly string? Versao =
        System.Reflection.Assembly.GetEntryAssembly()?
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion.Split('+')[0] is { Length: > 0 } v
            ? (v.Length > 12 ? v[..12] : v)
            : null;
}
