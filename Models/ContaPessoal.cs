namespace MapaMensal.Models;

public class ContaPessoal
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public DateOnly DataVencimento { get; set; }
    public DateOnly? DataPagamento { get; set; }
    public decimal ValorPrevisto { get; set; }
    public decimal? ValorPago { get; set; }
    public bool Pago { get; set; } = false;
    public string? MetodoPagamento { get; set; }  // "Dinheiro" | "Cartão" | null
    public Guid? GrupoRecorrencia { get; set; }
    public int RecorrenciaAtual { get; set; } = 1;
    public int TotalRecorrencias { get; set; } = 1;
    public int MesReferencia { get; set; }
    public int AnoReferencia { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
