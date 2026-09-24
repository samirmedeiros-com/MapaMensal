using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ZoomLog.Cliente;

/// Ligar numa aplicação são duas linhas no Program.cs:
///
///     builder.AddZoomLog();
///     …
///     app.UseRouting();
///     app.UseAuthentication();
///     app.UseZoomLog();          // depois da autenticação, antes dos controladores
///
/// e a secção `ZoomLog` no appsettings (ou `ZoomLog__Endereco` e
/// `ZoomLog__Chave` no segredo do Kubernetes).
public static class ZoomLogExtensoes
{
    public static WebApplicationBuilder AddZoomLog(
        this WebApplicationBuilder builder, Action<ZoomLogOpcoes>? configurar = null)
    {
        builder.Services.AddZoomLog(builder.Configuration, configurar);
        return builder;
    }

    public static IServiceCollection AddZoomLog(
        this IServiceCollection servicos, IConfiguration config, Action<ZoomLogOpcoes>? configurar = null)
    {
        var opcoes = servicos.AddOptions<ZoomLogOpcoes>().Bind(config.GetSection(ZoomLogOpcoes.Seccao));
        if (configurar is not null) opcoes.Configure(configurar);

        servicos.AddHttpContextAccessor();
        servicos.TryAddSingleton<ConsolaZoomLog>();
        servicos.TryAddSingleton<FilaZoomLog>();
        servicos.TryAddSingleton(sp => new Mascara(sp.GetRequiredService<IOptions<ZoomLogOpcoes>>().Value.CamposSensiveis));
        servicos.AddHostedService<EnviadorZoomLog>();
        servicos.AddSingleton<ILoggerProvider, RegistadorZoomLog>();

        // Todos os HttpClient da fábrica passam a registar as suas chamadas.
        servicos.AddTransient<ChamadasZoomLog>();
        servicos.ConfigureHttpClientDefaults(b => b.AddHttpMessageHandler<ChamadasZoomLog>());

        Rastreio.Ligar();
        return servicos;
    }

    public static IApplicationBuilder UseZoomLog(this IApplicationBuilder app) =>
        app.UseMiddleware<PedidosZoomLog>();

    /// No fim de um processo que corre e sai sem `app.Run()` (um CronJob):
    ///
    ///     await TarefaDataJud.CorrerAsync(…);
    ///     await app.Services.EnviarZoomLogAsync();
    ///     return;
    public static async Task EnviarZoomLogAsync(this IServiceProvider servicos)
    {
        var enviador = servicos.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<EnviadorZoomLog>().FirstOrDefault();
        if (enviador is not null) await enviador.EsvaziarAsync();
    }

    /// Para um `HttpClient` construído à mão, fora da fábrica:
    /// `new HttpClient(ZoomLogExtensoes.Handler(sp, new HttpClientHandler()))`.
    ///
    /// Constrói-se aqui e não se pede ao contentor: um handler é descartável,
    /// e um descartável transitório pedido ao fornecedor raiz fica guardado
    /// por ele até o processo acabar — uma fuga por cada chamada feita assim.
    public static HttpMessageHandler Handler(IServiceProvider sp, HttpMessageHandler interior) =>
        new ChamadasZoomLog(sp.GetRequiredService<FilaZoomLog>(), sp.GetRequiredService<Mascara>())
        {
            InnerHandler = interior,
        };
}

/// Garante que há `Activity` em cada pedido e em cada chamada. Sem ninguém a
/// ouvir, o .NET poupa-se a criá-las — e sem elas não há `TraceId`, não há
/// `traceparent` a atravessar para a aplicação seguinte, e cada evento fica
/// solto em vez de pendurado no seu pedido.
public static class Rastreio
{
    public static readonly ActivitySource Fonte = new("ZoomLog.Tarefas");

    private static ActivityListener? ouvinte;

    internal static void Ligar()
    {
        if (ouvinte is not null) return;

        ouvinte = new ActivityListener
        {
            ShouldListenTo = f => f.Name is "Microsoft.AspNetCore" or "System.Net.Http" or "ZoomLog.Tarefas",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.PropagationData,
        };
        ActivitySource.AddActivityListener(ouvinte);
    }
}

/// Uma tarefa fora de um pedido — um CronJob, um BackgroundService, um passo
/// de uma fila — com o seu próprio trace, para as chamadas que fizer ficarem
/// penduradas nela:
///
///     using var tarefa = TarefaZoomLog.Iniciar(fila, "Sincronizar DataJud");
///     … chamadas, logs …
///     tarefa.Falhou(e);   // opcional
public sealed class TarefaZoomLog : IDisposable
{
    private readonly FilaZoomLog fila;
    private readonly Activity? atividade;
    private readonly Stopwatch relogio = Stopwatch.StartNew();
    private readonly string nome;
    private Exception? falha;

    private TarefaZoomLog(FilaZoomLog fila, string nome)
    {
        this.fila = fila;
        this.nome = nome;
        // Uma tarefa começa um trace novo, mesmo que alguém a tenha lançado
        // de dentro de um pedido: o pedido acaba e ela continua.
        var anterior = Activity.Current;
        Activity.Current = null;
        atividade = Rastreio.Fonte.StartActivity(nome, ActivityKind.Internal);
        if (atividade is null) Activity.Current = anterior;
    }

    public static TarefaZoomLog Iniciar(FilaZoomLog fila, string nome) => new(fila, nome);

    public string? TraceId => atividade?.TraceId.ToHexString();

    public void Falhou(Exception e) => falha = e;

    public void Dispose()
    {
        var duracao = (int)relogio.ElapsedMilliseconds;
        fila.Escrever(new EventoZoomLog
        {
            Tipo = TipoEvento.Tarefa,
            Nivel = falha is null ? NivelEvento.Info : NivelEvento.Erro,
            Categoria = "Tarefa",
            Caminho = nome,
            Mensagem = falha is null ? $"{nome} terminou em {duracao} ms" : $"{nome} falhou em {duracao} ms: {falha.Message}",
            DuracaoMs = duracao,
            TraceId = atividade?.TraceId.ToHexString(),
            SpanId = atividade?.SpanId.ToHexString(),
            ExcecaoTipo = falha?.GetType().FullName,
            Excecao = falha?.ToString(),
        });
        atividade?.Dispose();
    }
}
