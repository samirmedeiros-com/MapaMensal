namespace MapaMensal.Models;

public class ContaPessoal
{
    public int Id { get; set; }

    /// <summary>"Entrada" ou "Saida".</summary>
    public string Tipo { get; set; } = "Saida";
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

    /// <summary>Preenchido quando o lançamento foi gerado automaticamente pela confirmação de recebimento de uma fatura.</summary>
    public int? TimesheetFaturaId { get; set; }

    /// <summary>Dados de pagamento por referência Multibanco — só fazem sentido em lançamentos de Saída.</summary>
    public string? Entidade { get; set; }
    public string? Referencia { get; set; }

    /// <summary>PDF ou imagem da fatura/recibo carregado pelo utilizador, guardado em base64.</summary>
    public string? AnexoBase64 { get; set; }
    public string? AnexoMimeType { get; set; }

    /// <summary>Moeda do lançamento — "EUR" (padrão) ou "BRL". ValorPrevisto está sempre em EUR;
    /// se a moeda original for BRL, ValorOriginal guarda o valor em reais e Observacoes a nota da conversão.</summary>
    public string Moeda { get; set; } = "EUR";
    public decimal? ValorOriginal { get; set; }
    public string? Observacoes { get; set; }
}
