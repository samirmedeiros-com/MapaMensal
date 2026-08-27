namespace MapaMensal.Models;

public class TarefaComentario
{
    public int Id { get; set; }
    public int TarefaId { get; set; }
    public Tarefa Tarefa { get; set; } = null!;
    public string Texto { get; set; } = string.Empty;
    /// <summary>Username de quem escreveu, guardado no momento — não é uma chave para mapa_users,
    /// para o comentário sobreviver à remoção do utilizador.</summary>
    public string Autor { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
