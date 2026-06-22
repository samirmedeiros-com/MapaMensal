namespace MapaMensal.Models;

public class Holiday
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsNational { get; set; } = true;
}
