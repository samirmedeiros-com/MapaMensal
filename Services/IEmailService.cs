namespace MapaMensal.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);

    Task SendConviteCompromissoAsync(string to, string nomeDestinatario,
        string tituloCompromisso, DateTime inicio, DateTime fim,
        string local, string? descricao, string? linkAceitar = null);

    Task SendConviteAlteradoAsync(string to, string nomeDestinatario,
        string tituloCompromisso, DateTime inicio, DateTime fim,
        string local, string? descricao, string? linkAceitar = null);

    Task SendConfirmacaoPublicaAsync(string to, string nomeParticipante,
        string titulo, DateTime inicio, DateTime fim, string local, string? linkIcs = null);
}
