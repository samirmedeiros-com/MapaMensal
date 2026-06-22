namespace MapaMensal.Models;

public class Tarefa
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string Status { get; set; } = "Backlog"; // Backlog | EmProgresso | Concluido
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateOnly? DataEntrega { get; set; }
    public decimal HorasGastas { get; set; }
    public bool Arquivado { get; set; } = false;
}
