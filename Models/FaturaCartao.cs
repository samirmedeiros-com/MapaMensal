namespace MapaMensal.Models;

public class FaturaCartao
{
    public int Id { get; set; }
    public int CartaoId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>"Aberta" (a receber lançamentos) ou "Fechada" (já lançada no Financeiro).</summary>
    public string Estado { get; set; } = "Aberta";

    public DateTime? DataFechamento { get; set; }

    /// <summary>Total da fatura na moeda do cartão, guardado no fecho.</summary>
    public decimal? ValorTotalMoedaCartao { get; set; }

    /// <summary>Total convertido para EUR, guardado no fecho (igual a ValorTotalMoedaCartao se o cartão for EUR).</summary>
    public decimal? ValorTotalEur { get; set; }

    /// <summary>Lançamento criado no Financeiro ao fechar a fatura.</summary>
    public int? ContaPessoalId { get; set; }

    /// <summary>Reflete o pagamento do lançamento associado no Financeiro — "NaoPago", "Parcial" ou "Pago".</summary>
    public string PagamentoStatus { get; set; } = "NaoPago";

    /// <summary>Valor (em EUR) efetivamente pago, preenchido quando o lançamento no Financeiro é marcado como pago.</summary>
    public decimal? ValorPagoEur { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
