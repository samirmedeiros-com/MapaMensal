namespace MapaMensal.Models;

public class CompromissoParticipante
{
    public int Id { get; set; }
    public int CompromissoId { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Telefone { get; set; }
    public string? CodigoPais { get; set; }
    public bool Notificar { get; set; }
    public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public bool Aceite { get; set; }
    public DateTime? AceiteEm { get; set; }
}
