using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Configuration;

namespace MapaMensal.Services;

public class EmailService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<EmailService> logger) : IEmailService
{
    private string ApiKey => config["SimplySend:ApiKey"] ?? "";
    private string AccountId => config["SimplySend:AccountId"] ?? "";
    private string From => config["SimplySend:From"] ?? "";
    private string SenderName => config["SimplySend:SenderName"] ?? "Mapa Mensal";
    private string BaseUrl => config["SimplySend:BaseUrl"] ?? "https://tapi.simplysend.email";

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var html = htmlBody + @"
<br><br>
<p style=""font-size:11px;color:#9ca3af"">
{{company_address_html}}<br>
{{unsubscribe_email_html}} &nbsp;|&nbsp; {{report_abuse_email_html}}
</p>";

        var payload = new Dictionary<string, object>
        {
            ["to"]        = to,
            ["from"]      = From,
            ["from_name"] = SenderName,
            ["subject"]   = subject,
            ["html"]      = html
        };

        var json = JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        logger.LogInformation("SimplySend payload: {json}", json);

        var client = httpFactory.CreateClient("simplysend");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        client.DefaultRequestHeaders.Add("X-Id", AccountId);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{BaseUrl.TrimEnd('/')}/send", content);

        var responseBody = await response.Content.ReadAsStringAsync();
        logger.LogInformation("SimplySend response {status}: {body}", (int)response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"SimplySend: erro {(int)response.StatusCode} — {responseBody}");
    }

    public async Task SendConviteCompromissoAsync(string to, string nomeDestinatario,
        string tituloCompromisso, DateTime inicio, DateTime fim,
        string local, string? descricao, string? linkAceitar = null)
    {
        var inicioStr = inicio.ToString("dd/MM/yyyy 'às' HH:mm");
        var fimStr = fim.ToString("HH:mm");
        var linkIcs = linkAceitar?.Replace("/aceitar/", "/ics/");
        var html = $@"
<div style=""font-family:Arial,sans-serif;max-width:600px;margin:0 auto"">
  <h2 style=""color:#534AB7"">Convite para compromisso</h2>
  <p>Olá <strong>{nomeDestinatario}</strong>,</p>
  <p>Foi adicionado/a como participante no seguinte compromisso:</p>
  <div style=""background:#EEEDFE;border-left:4px solid #534AB7;border-radius:6px;padding:16px 20px;margin:20px 0"">
    <p style=""margin:0 0 8px;font-size:18px;font-weight:600;color:#3C3489"">{tituloCompromisso}</p>
    <p style=""margin:0 0 4px;color:#6B6A65"">📅 {inicioStr} – {fimStr}</p>
    {(string.IsNullOrEmpty(local) ? "" : $@"<p style=""margin:0 0 4px;color:#6B6A65"">📍 {local}</p>")}
    {(string.IsNullOrEmpty(descricao) ? "" : $@"<p style=""margin:8px 0 0;color:#6B6A65"">{descricao}</p>")}
  </div>
  {(linkAceitar is not null ? BtnPrimario(linkAceitar, "✓ Aceitar convite") : "")}
  {CalendarButtons(tituloCompromisso, descricao, inicio, fim, local, linkIcs)}
  <hr/>
  <p style=""color:#9E9D98;font-size:12px"">Mapa Mensal — Gestão Pessoal</p>
</div>";

        await SendAsync(to, $"Convite: {tituloCompromisso}", html);
    }

    public async Task SendConviteAlteradoAsync(string to, string nomeDestinatario,
        string tituloCompromisso, DateTime inicio, DateTime fim,
        string local, string? descricao, string? linkAceitar = null)
    {
        var inicioStr = inicio.ToString("dd/MM/yyyy 'às' HH:mm");
        var fimStr = fim.ToString("HH:mm");
        var linkIcs = linkAceitar?.Replace("/aceitar/", "/ics/");
        var html = $@"
<div style=""font-family:Arial,sans-serif;max-width:600px;margin:0 auto"">
  <h2 style=""color:#D85A30"">Compromisso actualizado</h2>
  <p>Olá <strong>{nomeDestinatario}</strong>,</p>
  <p>O seguinte compromisso foi alterado. Por favor verifique os novos detalhes:</p>
  <div style=""background:#FAECE7;border-left:4px solid #D85A30;border-radius:6px;padding:16px 20px;margin:20px 0"">
    <p style=""margin:0 0 8px;font-size:18px;font-weight:600;color:#854F0B"">{tituloCompromisso}</p>
    <p style=""margin:0 0 4px;color:#6B6A65"">📅 {inicioStr} – {fimStr}</p>
    {(string.IsNullOrEmpty(local) ? "" : $@"<p style=""margin:0 0 4px;color:#6B6A65"">📍 {local}</p>")}
    {(string.IsNullOrEmpty(descricao) ? "" : $@"<p style=""margin:8px 0 0;color:#6B6A65"">{descricao}</p>")}
  </div>
  {(linkAceitar is not null ? BtnPrimario(linkAceitar, "✓ Confirmar nova data", "#D85A30") : "")}
  {CalendarButtons(tituloCompromisso, descricao, inicio, fim, local, linkIcs)}
  <hr/>
  <p style=""color:#9E9D98;font-size:12px"">Mapa Mensal — Gestão Pessoal</p>
</div>";

        await SendAsync(to, $"Actualização: {tituloCompromisso}", html);
    }

    public async Task SendConfirmacaoPublicaAsync(string to, string nomeParticipante,
        string titulo, DateTime inicio, DateTime fim, string local, string? linkIcs = null)
    {
        var inicioStr = inicio.ToString("dd/MM/yyyy 'às' HH:mm");
        var fimStr = fim.ToString("HH:mm");
        var html = $@"
<div style=""font-family:Arial,sans-serif;max-width:600px;margin:0 auto"">
  <h2 style=""color:#534AB7"">Marcação confirmada!</h2>
  <p>Olá <strong>{nomeParticipante}</strong>,</p>
  <p>A sua marcação foi recebida com sucesso. Aguardamos por si!</p>
  <div style=""background:#EEEDFE;border-left:4px solid #534AB7;border-radius:6px;padding:16px 20px;margin:20px 0"">
    <p style=""margin:0 0 8px;font-size:18px;font-weight:600;color:#3C3489"">{titulo}</p>
    <p style=""margin:0 0 4px;color:#6B6A65"">📅 {inicioStr} – {fimStr}</p>
    {(string.IsNullOrEmpty(local) ? "" : $@"<p style=""margin:0 0 4px;color:#6B6A65"">📍 {local}</p>")}
  </div>
  {CalendarButtons(titulo, null, inicio, fim, local, linkIcs)}
  <hr/>
  <p style=""color:#9E9D98;font-size:12px"">Mapa Mensal — Gestão Pessoal</p>
</div>";

        await SendAsync(to, $"Marcação confirmada: {titulo}", html);
    }

    private static string BtnPrimario(string link, string texto, string cor = "#534AB7") =>
        $@"<div style=""margin:20px 0"">
  <a href=""{link}"" style=""display:inline-block;background:{cor};color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600"">
    {texto}
  </a>
</div>";

    private static string CalendarButtons(string titulo, string? descricao,
        DateTime inicio, DateTime fim, string local, string? linkIcs)
    {
        Func<string, string> enc = s => HttpUtility.UrlEncode(s) ?? "";
        var dtStart = inicio.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
        var dtEnd = fim.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
        var dtStartOl = inicio.ToString("yyyy-MM-ddTHH:mm:ss");
        var dtEndOl = fim.ToString("yyyy-MM-ddTHH:mm:ss");
        var desc = descricao ?? "";

        var googleUrl = $"https://www.google.com/calendar/render?action=TEMPLATE"
            + $"&text={enc(titulo)}"
            + $"&dates={dtStart}/{dtEnd}"
            + (string.IsNullOrEmpty(desc) ? "" : $"&details={enc(desc)}")
            + (string.IsNullOrEmpty(local) ? "" : $"&location={enc(local)}");

        var outlookUrl = $"https://outlook.live.com/calendar/0/deeplink/compose?path=/calendar/action/compose&rru=addevent"
            + $"&subject={enc(titulo)}"
            + $"&startdt={enc(dtStartOl)}"
            + $"&enddt={enc(dtEndOl)}"
            + (string.IsNullOrEmpty(desc) ? "" : $"&body={enc(desc)}")
            + (string.IsNullOrEmpty(local) ? "" : $"&location={enc(local)}");

        var btnStyle = "display:inline-block;border:1px solid #d1d5db;border-radius:6px;padding:8px 16px;margin:4px;text-decoration:none;font-size:13px;color:#374151;background:#fff";

        var icsBtn = linkIcs is not null
            ? $@"<a href=""{linkIcs}"" style=""{btnStyle}"">⬇ Descarregar .ics</a>"
            : "";

        return $@"<div style=""margin:20px 0"">
  <p style=""margin:0 0 10px;color:#6B6A65;font-size:13px"">Adicionar ao calendário:</p>
  <a href=""{googleUrl}"" target=""_blank"" style=""{btnStyle}"">📅 Google Calendar</a>
  <a href=""{outlookUrl}"" target=""_blank"" style=""{btnStyle}"">📅 Outlook</a>
  {icsBtn}
</div>";
    }
}
