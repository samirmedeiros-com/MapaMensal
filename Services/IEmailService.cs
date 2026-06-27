namespace MapaMensal.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody,
        byte[]? attachment = null, string? attachmentName = null, string? attachmentContentType = null);

    Task SendConviteCompromissoAsync(string to, string nomeDestinatario,
        string tituloCompromisso, DateTime inicio, DateTime fim,
        string local, string? descricao, byte[] icsBytes);

    Task SendConfirmacaoPublicaAsync(string to, string nomeParticipante,
        string titulo, DateTime inicio, DateTime fim,
        string local, byte[] icsBytes);
}
