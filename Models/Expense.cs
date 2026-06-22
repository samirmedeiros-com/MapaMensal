namespace MapaMensal.Models;

public class Expense
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}
