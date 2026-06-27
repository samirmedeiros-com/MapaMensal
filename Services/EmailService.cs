using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace MapaMensal.Services;

public class EmailService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<EmailService> logger) : IEmailService
{
    private string ApiKey => config["SimplySend:ApiKey"] ?? "";
    private string AccountId => config["SimplySend:AccountId"] ?? "";
    private string From => config["SimplySend:From"] ?? "";
    private string SenderName => config["SimplySend:SenderName"] ?? "Mapa Mensal";
    private string BaseUrl => config["SimplySend:BaseUrl"] ?? "https://tapi.simplysend.email";

    public async Task SendAsync(string to, string subject, string htmlBody,
        byte[]? attachment = null, string? attachmentName = null, string? attachmentContentType = null)
    {
        var html = htmlBody + @"
<br><br>
<p style=""font-size:11px;color:#9ca3af"">
{{company_address_html}}<br>
{{unsubscribe_email_html}} &nbsp;|&nbsp; {{report_abuse_email_html}}
</p>";

        // Build payload — attachments as array (SimplySend format)
        var basePayload = new Dictionary<string, object>
        {
            ["to"]        = to,
            ["from"]      = From,
            ["from_name"] = SenderName,
            ["subject"]   = subject,
            ["html"]      = html
        };

        if (attachment != null && attachmentName != null)
        {
            basePayload["attachment"] = new Dictionary<string, string>
            {
                ["filename"]     = attachmentName,
                ["content"]      = Convert.ToBase64String(attachment),
                ["content_type"] = attachmentContentType ?? "application/octet-stream"
            };
        }

        var json = JsonSerializer.Serialize(basePayload,
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
        string local, string? descricao, byte[] icsBytes, string? linkAceitar = null)
    {
        var inicioStr = inicio.ToString("dd/MM/yyyy 'às' HH:mm");
        var fimStr = fim.ToString("HH:mm");
        var btnAceitar = linkAceitar is not null
            ? $@"<div style=""margin:20px 0"">
    <a href=""{linkAceitar}"" style=""display:inline-block;background:#534AB7;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600"">
      ✓ Aceitar convite
    </a>
  </div>"
            : "";
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
  {btnAceitar}
  <p style=""color:#6B6A65;font-size:13px"">O ficheiro em anexo permite adicionar este compromisso ao seu calendário.</p>
  <hr/>
  <p style=""color:#9E9D98;font-size:12px"">Mapa Mensal — Gestão Pessoal</p>
</div>";

        await SendAsync(to, $"Convite: {tituloCompromisso}", html, icsBytes, "compromisso.ics", "text/calendar");
    }

    public async Task SendConviteAlteradoAsync(string to, string nomeDestinatario,
        string tituloCompromisso, DateTime inicio, DateTime fim,
        string local, string? descricao, byte[] icsBytes, string? linkAceitar = null)
    {
        var inicioStr = inicio.ToString("dd/MM/yyyy 'às' HH:mm");
        var fimStr = fim.ToString("HH:mm");
        var btnAceitar = linkAceitar is not null
            ? $@"<div style=""margin:20px 0"">
    <a href=""{linkAceitar}"" style=""display:inline-block;background:#534AB7;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600"">
      ✓ Confirmar nova data
    </a>
  </div>"
            : "";
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
  {btnAceitar}
  <p style=""color:#6B6A65;font-size:13px"">O ficheiro em anexo actualiza o compromisso no seu calendário.</p>
  <hr/>
  <p style=""color:#9E9D98;font-size:12px"">Mapa Mensal — Gestão Pessoal</p>
</div>";

        await SendAsync(to, $"Actualização: {tituloCompromisso}", html, icsBytes, "compromisso.ics", "text/calendar");
    }

    public async Task SendConfirmacaoPublicaAsync(string to, string nomeParticipante,
        string titulo, DateTime inicio, DateTime fim,
        string local, byte[] icsBytes)
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
  <p>Clique no ficheiro em anexo para adicionar esta marcação ao seu calendário.</p>
  <hr/>
  <p style=""color:#9E9D98;font-size:12px"">Mapa Mensal — Gestão Pessoal</p>
</div>";

        await SendAsync(to, $"Marcação confirmada: {titulo}", html, icsBytes, "marcacao.ics", "text/calendar");
    }
}
