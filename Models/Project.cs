namespace MapaMensal.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Client { get; set; }
    public decimal DailyRate { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // Dados de faturação (compatíveis com a emissão de fatura via TocOnline)
    public string? FaturacaoNomeFiscal { get; set; }
    public string? FaturacaoNif { get; set; }
    public string? FaturacaoMorada { get; set; }
    public string? FaturacaoCodigoPostal { get; set; }
    public string? FaturacaoLocalidade { get; set; }
    public string FaturacaoPais { get; set; } = "PT";

    /// <summary>Dia do mês (1-31) em que a fatura vence. Usado para gerar a previsão de pagamento no
    /// Financeiro: se esse dia ainda não tiver passado no mês da emissão, a previsão fica nesse mês;
    /// caso contrário passa para o mês seguinte — nunca antes da data de emissão.</summary>
    public int DiaVencimento { get; set; } = 30;

    public ICollection<WorkDay> WorkDays { get; set; } = [];
}
