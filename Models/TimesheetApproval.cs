namespace MapaMensal.Models;

public class TimesheetApproval
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public bool IsApproved { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedByUsername { get; set; }
}
