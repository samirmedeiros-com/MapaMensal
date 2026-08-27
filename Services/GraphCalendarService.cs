using System.Text;
using System.Text.Json;

namespace MapaMensal.Services;

/// <summary>Cria/atualiza/remove lembretes no calendário Office 365 do utilizador (via Microsoft Graph,
/// autenticação client-credentials/app-only), para os lançamentos do Financeiro marcados como "Lembrete no calendário".</summary>
public class GraphCalendarService(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<GraphCalendarService> logger)
{
    private readonly string? _tenantId = config["Graph:TenantId"];
    private readonly string? _clientId = config["Graph:ClientId"];
    private readonly string? _clientSecret = config["Graph:ClientSecret"];
    private readonly string _mailbox = config["Graph:CalendarMailbox"] ?? "samir@samirmedeiros.com";

    private string? _tokenCache;
    private DateTime _tokenExpira = DateTime.MinValue;

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(_tenantId) && !string.IsNullOrWhiteSpace(_clientId) && !string.IsNullOrWhiteSpace(_clientSecret);

    private async Task<string> ObterTokenAsync(CancellationToken ct)
    {
        if (_tokenCache is not null && DateTime.UtcNow < _tokenExpira) return _tokenCache;

        var client = httpClientFactory.CreateClient("graph-auth");
        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId!,
            ["client_secret"] = _clientSecret!,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        });
        var resposta = await client.PostAsync($"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token", body, ct);
        resposta.EnsureSuccessStatusCode();

        using var stream = await resposta.Content.ReadAsStreamAsync(ct);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var token = doc.RootElement.GetProperty("access_token").GetString()!;
        var expiraEm = doc.RootElement.GetProperty("expires_in").GetInt32();

        _tokenCache = token;
        _tokenExpira = DateTime.UtcNow.AddSeconds(expiraEm - 60);
        return token;
    }

    private async Task<HttpClient> ClienteAutenticadoAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("graph");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await ObterTokenAsync(ct));
        return client;
    }

    /// <summary>Cria um evento de dia inteiro na data de vencimento e devolve o id do evento no Graph.</summary>
    public async Task<string> CriarLembreteAsync(string assunto, DateOnly data, string? corpo, CancellationToken ct = default)
    {
        var client = await ClienteAutenticadoAsync(ct);
        var payload = new
        {
            subject = assunto,
            body = new { contentType = "Text", content = corpo ?? "" },
            start = new { dateTime = data.ToString("yyyy-MM-dd"), timeZone = "UTC" },
            end = new { dateTime = data.AddDays(1).ToString("yyyy-MM-dd"), timeZone = "UTC" },
            isAllDay = true,
            isReminderOn = true,
            reminderMinutesBeforeStart = 1080 // 18h antes — padrão do Outlook para eventos de dia inteiro
        };

        var conteudo = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8);
        conteudo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var resposta = await client.PostAsync($"https://graph.microsoft.com/v1.0/users/{_mailbox}/events", conteudo, ct);
        var texto = await resposta.Content.ReadAsStringAsync(ct);
        if (!resposta.IsSuccessStatusCode)
        {
            logger.LogError("Falha ao criar lembrete no calendário: {Status} {Texto}", resposta.StatusCode, texto);
            throw new InvalidOperationException("Não foi possível criar o lembrete no calendário.");
        }

        using var doc = JsonDocument.Parse(texto);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task AtualizarLembreteAsync(string eventId, string assunto, DateOnly data, string? corpo, CancellationToken ct = default)
    {
        var client = await ClienteAutenticadoAsync(ct);
        var payload = new
        {
            subject = assunto,
            body = new { contentType = "Text", content = corpo ?? "" },
            start = new { dateTime = data.ToString("yyyy-MM-dd"), timeZone = "UTC" },
            end = new { dateTime = data.AddDays(1).ToString("yyyy-MM-dd"), timeZone = "UTC" }
        };
        var conteudo = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8);
        conteudo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var resposta = await client.PatchAsync($"https://graph.microsoft.com/v1.0/users/{_mailbox}/events/{eventId}", conteudo, ct);
        if (!resposta.IsSuccessStatusCode)
        {
            var texto = await resposta.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Falha ao atualizar lembrete {EventId}: {Status} {Texto}", eventId, resposta.StatusCode, texto);
        }
    }

    public async Task RemoverLembreteAsync(string eventId, CancellationToken ct = default)
    {
        var client = await ClienteAutenticadoAsync(ct);
        var resposta = await client.DeleteAsync($"https://graph.microsoft.com/v1.0/users/{_mailbox}/events/{eventId}", ct);
        if (!resposta.IsSuccessStatusCode && resposta.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var texto = await resposta.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Falha ao remover lembrete {EventId}: {Status} {Texto}", eventId, resposta.StatusCode, texto);
        }
    }
}
