namespace MapaMensal.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Client { get; set; }
    public decimal DailyRate { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<WorkDay> WorkDays { get; set; } = [];
}
