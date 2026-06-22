namespace MapaMensal.Models;

public class CategoriaContaPessoal
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Cor { get; set; }
    public int Ordem { get; set; } = 0;
}
