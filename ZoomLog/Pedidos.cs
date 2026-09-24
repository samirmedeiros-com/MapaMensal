using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ZoomLog.Cliente;

/// Um evento por pedido que entra: quem, o quê, quanto tempo, como acabou.
///
/// Fica logo a seguir ao `UseRouting` (para conhecer a rota) e depois da
/// autenticação (para saber quem é). Ver `UseZoomLog`.
public sealed class PedidosZoomLog(RequestDelegate seguinte, FilaZoomLog fila, Mascara mascara)
{
    private readonly ZoomLogOpcoes o = fila.Opcoes;

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!o.Ativo || !Registado(ctx.Request.Path))
        {
            await seguinte(ctx);
            return;
        }

        var corpos = o.CorposEntrada.Any(p => ctx.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
        var pedidoPequeno = ctx.Request.ContentLength is > 0 and <= 256 * 1024 && Mascara.ETexto(ctx.Request.ContentType);

        // Guardar o corpo do pedido obriga a poder lê-lo duas vezes. Só se
        // paga isso quando pode vir a ser preciso — e nunca num upload.
        if (pedidoPequeno && (corpos || o.CorpoEntradaEmErro))
            ctx.Request.EnableBuffering();

        Stream? original = null;
        MemoryStream? copia = null;
        if (corpos)
        {
            original = ctx.Response.Body;
            copia = new MemoryStream();
            ctx.Response.Body = new Espelho(original, copia, o.CorpoMaximo * 4);
        }

        var inicio = Stopwatch.GetTimestamp();
        Exception? falha = null;

        try
        {
            await seguinte(ctx);
        }
        catch (Exception e)
        {
            falha = e;
            throw;
        }
        finally
        {
            if (original is not null) ctx.Response.Body = original;

            try
            {
                await Registar(ctx, inicio, falha, corpos, pedidoPequeno, copia);
            }
            catch
            {
                // O registo nunca pode ser o motivo de um pedido falhar.
            }
            finally
            {
                copia?.Dispose();
            }
        }
    }

    private bool Registado(PathString caminho) =>
        o.CaminhosRegistados.Any(p => caminho.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase))
        && !o.CaminhosIgnorados.Any(p => caminho.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));

    private async Task Registar(
        HttpContext ctx, long inicio, Exception? falha, bool corpos, bool pedidoPequeno, MemoryStream? copia)
    {
        var duracao = (int)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
        // Uma exceção que escapou é um 500, mesmo que a resposta ainda diga 200:
        // quem o transforma em 500 é um middleware de fora deste.
        var estado = falha is not null ? 500 : ctx.Response.StatusCode;
        var atividade = Activity.Current;
        var rota = (ctx.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;

        var evento = new EventoZoomLog
        {
            Tipo = TipoEvento.Pedido,
            Nivel = estado >= 500 ? NivelEvento.Erro : estado >= 400 ? NivelEvento.Aviso : NivelEvento.Info,
            Categoria = "Pedido",
            Metodo = ctx.Request.Method,
            Destino = ctx.Request.Host.Host,
            Caminho = ctx.Request.Path + mascara.Texto(ctx.Request.QueryString.Value),
            Rota = rota is null ? null : "/" + rota.TrimStart('/'),
            Estado = estado,
            DuracaoMs = duracao,
            TraceId = atividade?.TraceId.ToHexString(),
            SpanId = atividade?.SpanId.ToHexString(),
            PaiId = atividade?.ParentSpanId is { } pai && pai != default ? pai.ToHexString() : null,
            Utilizador = Identificacao.Utilizador(ctx, o),
            Cliente = Identificacao.Cliente(ctx, o),
            Ip = IpCliente.De(ctx),
        };

        evento.Mensagem = $"{evento.Metodo} {evento.Rota ?? ctx.Request.Path.Value} → {estado} em {duracao} ms";

        if (falha is not null)
        {
            evento.ExcecaoTipo = falha.GetType().FullName;
            evento.Excecao = falha.ToString();
            evento.Mensagem += $" — {falha.Message}";
        }

        var agent = ctx.Request.Headers.UserAgent.ToString();
        if (agent.Length > 0)
            evento.Propriedades = new() { ["userAgent"] = agent.Length > 300 ? agent[..300] : agent };

        if (corpos || (o.CorpoEntradaEmErro && estado >= 500))
            evento.Corpo = await Corpo(ctx, pedidoPequeno, copia);

        fila.Escrever(evento);
    }

    private async Task<CorpoZoomLog> Corpo(HttpContext ctx, bool pedidoPequeno, MemoryStream? copia)
    {
        var corpo = new CorpoZoomLog
        {
            PedidoCabecalhos = mascara.Cabecalhos(ctx.Request.Headers.Select(h =>
                new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value.Select(v => v ?? "")))),
            TipoPedido = ctx.Request.ContentType,
            PedidoTamanho = ctx.Request.ContentLength,
            RespostaCabecalhos = mascara.Cabecalhos(ctx.Response.Headers.Select(h =>
                new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value.Select(v => v ?? "")))),
            TipoResposta = ctx.Response.ContentType,
            RespostaTamanho = ctx.Response.ContentLength ?? copia?.Length,
        };

        var truncado = false;

        if (pedidoPequeno && ctx.Request.Body.CanSeek)
        {
            ctx.Request.Body.Position = 0;
            using var leitor = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
            var texto = await leitor.ReadToEndAsync();
            corpo.Pedido = mascara.Texto(Mascara.Cortar(texto, o.CorpoMaximo, ref truncado));
        }

        if (copia is { Length: > 0 } && Mascara.ETexto(ctx.Response.ContentType))
        {
            var texto = Encoding.UTF8.GetString(copia.GetBuffer(), 0, (int)copia.Length);
            if (copia.Length >= o.CorpoMaximo * 4) truncado = true;
            corpo.Resposta = mascara.Texto(Mascara.Cortar(texto, o.CorpoMaximo, ref truncado));
        }

        corpo.Truncado = truncado;
        return corpo;
    }

    /// Escreve na resposta verdadeira e guarda uma cópia até um limite. A
    /// resposta sai para o browser à mesma velocidade — não se espera pelo
    /// fim para a mandar.
    private sealed class Espelho(Stream destino, MemoryStream copia, int limite) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => destino.Flush();
        public override Task FlushAsync(CancellationToken ct) => destino.FlushAsync(ct);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            Copiar(buffer.AsSpan(offset, count));
            destino.Write(buffer, offset, count);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            Copiar(buffer.Span);
            await destino.WriteAsync(buffer, ct);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            WriteAsync(buffer.AsMemory(offset, count), ct).AsTask();

        private void Copiar(ReadOnlySpan<byte> bytes)
        {
            var cabe = (int)Math.Min(bytes.Length, limite - copia.Length);
            if (cabe > 0) copia.Write(bytes[..cabe]);
        }
    }
}

/// Quem está a usar a aplicação e de que cliente é o pedido, lidos dos claims.
/// O mesmo para o evento do pedido e para os logs escritos durante ele.
public static class Identificacao
{
    /// Os nomes que as aplicações da casa usam para o inquilino.
    private static readonly string[] ClaimsDeCliente =
        ["empresa", "empresaNome", "empresaId", "escritorio", "cliente", "clienteId", "tenant", "tenantId", "entidade", "entidadeId"];

    public static string? Utilizador(HttpContext ctx, ZoomLogOpcoes o)
    {
        if (o.UtilizadorDe is not null) return o.UtilizadorDe(ctx);
        var u = ctx.User;
        if (u.Identity?.IsAuthenticated != true) return null;

        return u.FindFirstValue(ClaimTypes.Email)
            ?? u.FindFirstValue("email")
            ?? u.FindFirstValue(ClaimTypes.Name)
            ?? u.FindFirstValue("name")
            ?? u.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? u.FindFirstValue("sub");
    }

    public static string? Cliente(HttpContext ctx, ZoomLogOpcoes o)
    {
        if (o.ClienteDe is not null) return o.ClienteDe(ctx);
        if (ctx.User.Identity?.IsAuthenticated != true) return null;

        foreach (var nome in ClaimsDeCliente)
            if (ctx.User.FindFirstValue(nome) is { Length: > 0 } v)
                return v;

        return null;
    }
}
