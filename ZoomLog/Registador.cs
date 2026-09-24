using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ZoomLog.Cliente;

/// O `ILogger` da aplicação, também para o ZoomLog. Nada muda no código de
/// quem regista: `registo.LogWarning("Falhou {Numero}", n)` passa a aparecer
/// lá, pendurado no pedido em que aconteceu.
[ProviderAlias("ZoomLog")]
public sealed class RegistadorZoomLog(FilaZoomLog fila, Mascara mascara, IHttpContextAccessor contexto)
    : ILoggerProvider
{
    public ILogger CreateLogger(string categoria) => new Registo(categoria, this);

    public void Dispose() { }

    private bool Ligado(string categoria, LogLevel nivel)
    {
        var o = fila.Opcoes;
        if (!o.Ativo || nivel == LogLevel.None) return false;

        // Estes já têm evento próprio (pedido, chamada) ou são o próprio envio.
        if (categoria.StartsWith("ZoomLog.Cliente", StringComparison.Ordinal)
            || categoria.StartsWith("System.Net.Http.HttpClient", StringComparison.Ordinal)
            || categoria.StartsWith("Microsoft.AspNetCore.Hosting.Diagnostics", StringComparison.Ordinal)
            || categoria.StartsWith("Microsoft.AspNetCore.Routing", StringComparison.Ordinal))
            return false;

        // A regra da categoria mais comprida que casar, como no appsettings.
        var minimo = o.NivelMinimo;
        var melhor = -1;
        foreach (var (prefixo, n) in o.NivelPorCategoria)
            if (prefixo.Length > melhor && categoria.StartsWith(prefixo, StringComparison.Ordinal))
            {
                minimo = n;
                melhor = prefixo.Length;
            }

        return nivel >= minimo;
    }

    private void Escrever<TState>(
        string categoria, LogLevel nivel, EventId id, TState estado, Exception? excecao,
        Func<TState, Exception?, string> formatar)
    {
        var atividade = Activity.Current;
        var evento = new EventoZoomLog
        {
            Tipo = TipoEvento.Log,
            Nivel = nivel switch
            {
                LogLevel.Trace or LogLevel.Debug => NivelEvento.Debug,
                LogLevel.Information => NivelEvento.Info,
                LogLevel.Warning => NivelEvento.Aviso,
                LogLevel.Error => NivelEvento.Erro,
                _ => NivelEvento.Critico,
            },
            Categoria = categoria,
            Mensagem = mascara.Texto(formatar(estado, excecao)) ?? "",
            TraceId = atividade?.TraceId.ToHexString(),
            SpanId = SpanDoPasso(atividade),
        };

        if (excecao is not null)
        {
            evento.ExcecaoTipo = excecao.GetType().FullName;
            evento.Excecao = excecao.ToString();
        }

        // Os valores do molde vão à parte: «Falhou {Numero}» com Numero=123
        // pesquisa-se por Numero, não por texto.
        if (estado is IReadOnlyList<KeyValuePair<string, object?>> valores)
        {
            var props = new Dictionary<string, string?>();
            foreach (var (k, v) in valores)
            {
                if (k == "{OriginalFormat}") props["modelo"] = v?.ToString();
                else props[k] = mascara.Sensivel(k) ? Mascara.Oculto : Curto(v?.ToString());
            }

            if (id.Id != 0) props["eventoId"] = id.Name is { Length: > 0 } nome ? $"{id.Id} {nome}" : id.Id.ToString();
            if (props.Count > 0) evento.Propriedades = props;
        }

        if (contexto.HttpContext is { } ctx)
        {
            evento.Utilizador = Identificacao.Utilizador(ctx, fila.Opcoes);
            evento.Cliente = Identificacao.Cliente(ctx, fila.Opcoes);
        }

        fila.Escrever(evento);
    }

    /// Dentro de um pedido, o passo é o do pedido que entrou — é aí que o log
    /// se pendura no ecrã. Um `HttpRequestOut` a decorrer não é passo nosso.
    private static string? SpanDoPasso(Activity? a)
    {
        while (a is not null && a.OperationName == "System.Net.Http.HttpRequestOut") a = a.Parent;
        return a?.SpanId.ToHexString();
    }

    private static string? Curto(string? v) => v is { Length: > 1000 } ? v[..1000] : v;

    private sealed class Registo(string categoria, RegistadorZoomLog dono) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel nivel) => dono.Ligado(categoria, nivel);

        public void Log<TState>(
            LogLevel nivel, EventId id, TState estado, Exception? excecao, Func<TState, Exception?, string> formatar)
        {
            if (!IsEnabled(nivel)) return;
            try
            {
                dono.Escrever(categoria, nivel, id, estado, excecao, formatar);
            }
            catch
            {
                // Um log que falha a montar não pode partir quem registou.
            }
        }
    }
}
