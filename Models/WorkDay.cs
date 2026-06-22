namespace MapaMensal.Models;

public class WorkDay
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>
    /// 1.0 = dia completo, 0.5 = meio dia, 0.0 = não trabalhou, -1 = férias
    /// </summary>
    public decimal Mark { get; set; }

    public Project Project { get; set; } = null!;
}
