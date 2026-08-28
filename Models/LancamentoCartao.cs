namespace MapaMensal.Models;

public class LancamentoCartao
{
    public int Id { get; set; }
    public int FaturaCartaoId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public DateOnly Data { get; set; }

    /// <summary>Valor já convertido para a moeda do cartão — é este que soma para o total da fatura.</summary>
    public decimal Valor { get; set; }

    /// <summary>Preenchido só quando o lançamento foi feito numa moeda diferente da do cartão.</summary>
    public decimal? ValorOriginal { get; set; }
    public string? MoedaOriginal { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
