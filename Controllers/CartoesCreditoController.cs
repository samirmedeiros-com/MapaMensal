using MapaMensal.Data;
using MapaMensal.Models;
using MapaMensal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartoesCreditoController(AppDbContext db, CurrencyService currency, ILogger<CartoesCreditoController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cartoes = await db.CartoesCredito.OrderBy(c => c.Nome).ToListAsync();
        return Ok(cartoes.Select(ToDtoAnon));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CartaoCreditoDto dto)
    {
        var c = new CartaoCredito
        {
            Nome = dto.Nome,
            Moeda = dto.Moeda == "BRL" ? "BRL" : "EUR",
            DiaVencimento = Math.Clamp(dto.DiaVencimento, 1, 31),
            Ativo = true
        };
        db.CartoesCredito.Add(c);
        await db.SaveChangesAsync();
        return Ok(ToDtoAnon(c));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CartaoCreditoDto dto)
    {
        var c = await db.CartoesCredito.FindAsync(id);
        if (c is null) return NotFound();
        c.Nome = dto.Nome;
        c.Moeda = dto.Moeda == "BRL" ? "BRL" : "EUR";
        c.DiaVencimento = Math.Clamp(dto.DiaVencimento, 1, 31);
        c.Ativo = dto.Ativo;
        await db.SaveChangesAsync();
        return Ok(ToDtoAnon(c));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await db.CartoesCredito.FindAsync(id);
        if (c is null) return NotFound();

        var temFaturas = await db.FaturasCartao.AnyAsync(f => f.CartaoId == id);
        if (temFaturas)
            return Conflict("Este cartão já tem faturas registadas — desative-o em vez de eliminar.");

        db.CartoesCredito.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Devolve a fatura do mês pedido, criando-a (em aberto) se ainda não existir.</summary>
    [HttpGet("{cartaoId}/faturas/{year}/{month}")]
    public async Task<IActionResult> GetOuCriarFatura(int cartaoId, int year, int month)
    {
        var cartao = await db.CartoesCredito.FindAsync(cartaoId);
        if (cartao is null) return NotFound("Cartão não encontrado.");

        var fatura = await db.FaturasCartao.FirstOrDefaultAsync(f => f.CartaoId == cartaoId && f.Year == year && f.Month == month);
        if (fatura is null)
        {
            fatura = new FaturaCartao { CartaoId = cartaoId, Year = year, Month = month, Estado = "Aberta" };
            db.FaturasCartao.Add(fatura);
            await db.SaveChangesAsync();
        }

        var lancamentos = await db.LancamentosCartao
            .Where(l => l.FaturaCartaoId == fatura.Id)
            .OrderBy(l => l.Data).ThenBy(l => l.Id)
            .ToListAsync();

        return Ok(new
        {
            fatura.Id,
            fatura.CartaoId,
            fatura.Year,
            fatura.Month,
            fatura.Estado,
            DataFechamento = fatura.DataFechamento?.ToString("yyyy-MM-dd"),
            fatura.ValorTotalMoedaCartao,
            fatura.ValorTotalEur,
            fatura.ContaPessoalId,
            fatura.PagamentoStatus,
            fatura.ValorPagoEur,
            DataVencimento = CalcularDataVencimento(year, month, cartao.DiaVencimento).ToString("yyyy-MM-dd"),
            Total = lancamentos.Sum(l => l.Valor),
            Lancamentos = lancamentos.Select(ToLancamentoDtoAnon)
        });
    }

    /// <summary>Histórico de faturas de um cartão (mais recentes primeiro).</summary>
    [HttpGet("{cartaoId}/faturas")]
    public async Task<IActionResult> GetHistorico(int cartaoId)
    {
        var faturas = await db.FaturasCartao
            .Where(f => f.CartaoId == cartaoId)
            .OrderByDescending(f => f.Year).ThenByDescending(f => f.Month)
            .ToListAsync();

        return Ok(faturas.Select(f => new
        {
            f.Id, f.Year, f.Month, f.Estado,
            DataFechamento = f.DataFechamento?.ToString("yyyy-MM-dd"),
            f.ValorTotalMoedaCartao, f.ValorTotalEur, f.ContaPessoalId,
            f.PagamentoStatus, f.ValorPagoEur
        }));
    }

    /// <summary>Pré-visualiza o total (na moeda do cartão e convertido para EUR) sem fechar a fatura.</summary>
    [HttpGet("faturas/{faturaId}/preview-fechamento")]
    public async Task<IActionResult> PreviewFechamento(int faturaId)
    {
        var fatura = await db.FaturasCartao.FindAsync(faturaId);
        if (fatura is null) return NotFound("Fatura não encontrada.");

        var cartao = await db.CartoesCredito.FindAsync(fatura.CartaoId);
        if (cartao is null) return NotFound("Cartão não encontrado.");

        var totalCartao = await db.LancamentosCartao.Where(l => l.FaturaCartaoId == faturaId).SumAsync(l => l.Valor);
        var totalEur = totalCartao;

        if (cartao.Moeda != "EUR")
        {
            try
            {
                var conversao = await currency.ConverterParaEurAsync(totalCartao, cartao.Moeda);
                totalEur = conversao.ValorConvertido;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao pré-visualizar conversão da fatura {FaturaId}.", faturaId);
                return UnprocessableEntity("Não foi possível obter a cotação da moeda. Tente novamente.");
            }
        }

        return Ok(new { TotalMoedaCartao = totalCartao, TotalEur = totalEur, Moeda = cartao.Moeda });
    }

    [HttpPost("faturas/{faturaId}/lancamentos")]
    public async Task<IActionResult> AdicionarLancamento(int faturaId, [FromBody] LancamentoCartaoDto dto)
    {
        var fatura = await db.FaturasCartao.FindAsync(faturaId);
        if (fatura is null) return NotFound("Fatura não encontrada.");
        if (fatura.Estado != "Aberta") return UnprocessableEntity("Esta fatura já está fechada.");

        var cartao = await db.CartoesCredito.FindAsync(fatura.CartaoId);
        if (cartao is null) return NotFound("Cartão não encontrado.");

        var moedaLancamento = string.IsNullOrWhiteSpace(dto.Moeda) ? cartao.Moeda : dto.Moeda.ToUpperInvariant();
        if (cartao.Moeda == "EUR" && moedaLancamento != "EUR")
            return UnprocessableEntity("Este cartão é em EUR — só são aceites lançamentos em EUR.");

        var lancamento = new LancamentoCartao
        {
            FaturaCartaoId = faturaId,
            Descricao = dto.Descricao,
            Categoria = dto.Categoria,
            Data = string.IsNullOrWhiteSpace(dto.Data) ? DateOnly.FromDateTime(DateTime.UtcNow) : DateOnly.Parse(dto.Data)
        };

        if (moedaLancamento == cartao.Moeda)
        {
            lancamento.Valor = dto.Valor;
        }
        else
        {
            try
            {
                var conversao = await currency.ConverterAsync(dto.Valor, moedaLancamento, cartao.Moeda);
                lancamento.Valor = conversao.ValorConvertido;
                lancamento.ValorOriginal = dto.Valor;
                lancamento.MoedaOriginal = moedaLancamento;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao converter lançamento de {MoedaOrigem} para {MoedaCartao}.", moedaLancamento, cartao.Moeda);
                return UnprocessableEntity("Não foi possível obter a cotação da moeda. Tente novamente.");
            }
        }

        db.LancamentosCartao.Add(lancamento);
        await db.SaveChangesAsync();
        return Ok(ToLancamentoDtoAnon(lancamento));
    }

    [HttpDelete("lancamentos/{id}")]
    public async Task<IActionResult> RemoverLancamento(int id)
    {
        var lancamento = await db.LancamentosCartao.FindAsync(id);
        if (lancamento is null) return NotFound();

        var fatura = await db.FaturasCartao.FindAsync(lancamento.FaturaCartaoId);
        if (fatura is not null && fatura.Estado != "Aberta")
            return UnprocessableEntity("Esta fatura já está fechada.");

        db.LancamentosCartao.Remove(lancamento);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("faturas/{faturaId}/fechar")]
    public async Task<IActionResult> FecharFatura(int faturaId)
    {
        var fatura = await db.FaturasCartao.FindAsync(faturaId);
        if (fatura is null) return NotFound("Fatura não encontrada.");
        if (fatura.Estado != "Aberta") return UnprocessableEntity("Esta fatura já está fechada.");

        var cartao = await db.CartoesCredito.FindAsync(fatura.CartaoId);
        if (cartao is null) return NotFound("Cartão não encontrado.");

        var lancamentos = await db.LancamentosCartao.Where(l => l.FaturaCartaoId == faturaId).ToListAsync();
        var totalCartao = lancamentos.Sum(l => l.Valor);

        decimal totalEur = totalCartao;
        string? observacoes = null;
        if (cartao.Moeda != "EUR")
        {
            try
            {
                var conversao = await currency.ConverterParaEurAsync(totalCartao, cartao.Moeda);
                totalEur = conversao.ValorConvertido;
                observacoes = currency.GerarObservacao(conversao, cartao.Moeda);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao converter total da fatura {FaturaId} para EUR.", faturaId);
                return UnprocessableEntity("Não foi possível obter a cotação da moeda para fechar a fatura. Tente novamente.");
            }
        }

        var dataVencimento = CalcularDataVencimento(fatura.Year, fatura.Month, cartao.DiaVencimento);
        var contaPessoal = new ContaPessoal
        {
            Tipo = "Saida",
            Descricao = $"Fatura {cartao.Nome} — {fatura.Month:D2}/{fatura.Year}",
            Categoria = "Cartão de Crédito",
            DataVencimento = dataVencimento,
            ValorPrevisto = totalEur,
            MesReferencia = dataVencimento.Month,
            AnoReferencia = dataVencimento.Year,
            Moeda = cartao.Moeda,
            ValorOriginal = cartao.Moeda != "EUR" ? totalCartao : null,
            Observacoes = observacoes
        };
        db.ContasPessoais.Add(contaPessoal);
        await db.SaveChangesAsync();

        fatura.Estado = "Fechada";
        fatura.DataFechamento = DateTime.UtcNow;
        fatura.ValorTotalMoedaCartao = totalCartao;
        fatura.ValorTotalEur = totalEur;
        fatura.ContaPessoalId = contaPessoal.Id;
        await db.SaveChangesAsync();

        return Ok(new
        {
            fatura.Id, fatura.Estado, fatura.ValorTotalMoedaCartao, fatura.ValorTotalEur, fatura.ContaPessoalId
        });
    }

    /// <summary>A fatura de um mês vence sempre no mês seguinte (comportamento normal de cartão de crédito).</summary>
    private static DateOnly CalcularDataVencimento(int year, int month, int diaVencimento)
    {
        var proximo = new DateOnly(year, month, 1).AddMonths(1);
        var diasNoMes = DateTime.DaysInMonth(proximo.Year, proximo.Month);
        return new DateOnly(proximo.Year, proximo.Month, Math.Min(diaVencimento, diasNoMes));
    }

    private static object ToDtoAnon(CartaoCredito c) => new { c.Id, c.Nome, c.Moeda, c.DiaVencimento, c.Ativo };

    private static object ToLancamentoDtoAnon(LancamentoCartao l) => new
    {
        l.Id, l.FaturaCartaoId, l.Descricao, l.Categoria,
        Data = l.Data.ToString("yyyy-MM-dd"),
        l.Valor, l.ValorOriginal, l.MoedaOriginal
    };
}

public record CartaoCreditoDto(string Nome, string Moeda, int DiaVencimento, bool Ativo = true);

public record LancamentoCartaoDto(string Descricao, string Categoria, decimal Valor, string? Moeda = null, string? Data = null);
