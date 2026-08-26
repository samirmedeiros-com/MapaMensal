namespace MapaMensal.Models;

public class TimesheetFatura
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }

    public string NumeroFatura { get; set; } = string.Empty;
    public DateTime DataEmissao { get; set; }
    public string? PdfBase64 { get; set; }
    public string? TocOnlineDocId { get; set; }

    /// <summary>"Emitida" ou "Recebida".</summary>
    public string Estado { get; set; } = "Emitida";
    public DateTime? DataRecebimento { get; set; }

    /// <summary>"Online" (via TocOnline) ou "Offline" (carregada manualmente).</summary>
    public string Origem { get; set; } = "Online";

    public Project Project { get; set; } = null!;
}
