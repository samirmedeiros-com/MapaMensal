namespace MapaMensal.Models;

public class CartaoCredito
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>Moeda em que o cartão fatura — "EUR" ou "BRL". Os lançamentos ficam sempre
    /// acumulados nesta moeda; um lançamento numa moeda diferente é convertido ao ser criado.</summary>
    public string Moeda { get; set; } = "EUR";

    /// <summary>Dia do mês (1-31) em que a fatura vence.</summary>
    public int DiaVencimento { get; set; } = 10;

    public bool Ativo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
