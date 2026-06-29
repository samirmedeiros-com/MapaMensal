namespace MapaMensal.Models;

public enum TipoCompromisso { Pessoal = 0, Publico = 1, LembreteConta = 2 }
public enum StatusCompromisso { Agendado = 0, Cancelado = 1, Concluido = 2 }

public class Compromisso
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string? Descricao { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public int? ContaPessoalId { get; set; }
    public string Local { get; set; } = "";
    public bool Online { get; set; }
    public string? LinkOnline { get; set; }
    public TipoCompromisso Tipo { get; set; }
    public StatusCompromisso Status { get; set; }
    public bool NotificarParticipantes { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public string? RecorrenciaId { get; set; }
    public List<CompromissoParticipante> Participantes { get; set; } = new();
}
