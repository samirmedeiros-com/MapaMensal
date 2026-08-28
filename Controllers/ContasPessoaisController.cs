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
public class ContasPessoaisController(AppDbContext db, ClaudeService claude, CurrencyService currency, GraphCalendarService graph, ILogger<ContasPessoaisController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? inicio, [FromQuery] string? fim,
        [FromQuery] string? tipo, [FromQuery] bool? pago)
    {
        var query = db.ContasPessoais.AsQueryable();

        if (!string.IsNullOrEmpty(inicio))
        {
            var d = DateOnly.Parse(inicio);
            query = query.Where(c => c.DataVencimento >= d);
        }
        if (!string.IsNullOrEmpty(fim))
        {
            var d = DateOnly.Parse(fim);
            query = query.Where(c => c.DataVencimento <= d);
        }
        if (!string.IsNullOrEmpty(tipo))
            query = query.Where(c => c.Tipo == tipo);
        if (pago.HasValue)
            query = query.Where(c => c.Pago == pago.Value);

        var result = await query
            .OrderBy(c => c.DataVencimento)
            .ThenBy(c => c.Categoria)
            .Select(c => ToDtoAnon(c))
            .ToListAsync();

        return Ok(result);
    }

    [HttpGet("resumo")]
    public async Task<IActionResult> Resumo([FromQuery] string? inicio, [FromQuery] string? fim)
    {
        // Saldo total: posição de caixa acumulada de sempre (todos os movimentos já pagos/recebidos).
        var todasRealizadas = await db.ContasPessoais.Where(c => c.Pago).ToListAsync();
        var saldoTotal = todasRealizadas.Where(c => c.Tipo == "Entrada").Sum(c => c.ValorPago ?? 0)
                       - todasRealizadas.Where(c => c.Tipo == "Saida").Sum(c => c.ValorPago ?? 0);

        var query = db.ContasPessoais.AsQueryable();
        if (!string.IsNullOrEmpty(inicio))
        {
            var d = DateOnly.Parse(inicio);
            query = query.Where(c => c.DataVencimento >= d);
        }
        if (!string.IsNullOrEmpty(fim))
        {
            var d = DateOnly.Parse(fim);
            query = query.Where(c => c.DataVencimento <= d);
        }

        var noPeriodo = await query.ToListAsync();
        var entradas = noPeriodo.Where(c => c.Tipo == "Entrada").ToList();
        var saidas = noPeriodo.Where(c => c.Tipo == "Saida").ToList();

        var totalEntradas = entradas.Sum(c => c.ValorPrevisto);
        var totalSaidas = saidas.Sum(c => c.ValorPrevisto);
        var saldoReal = entradas.Where(c => c.Pago).Sum(c => c.ValorPago ?? 0)
                      - saidas.Where(c => c.Pago).Sum(c => c.ValorPago ?? 0);
        var previsaoEntradas = entradas.Where(c => !c.Pago).Sum(c => c.ValorPrevisto);
        var previsaoDespesas = saidas.Where(c => !c.Pago).Sum(c => c.ValorPrevisto);

        var porCategoria = saidas
            .GroupBy(c => c.Categoria)
            .Select(g => new { Categoria = g.Key, Total = g.Sum(c => c.ValorPrevisto) })
            .OrderByDescending(x => x.Total);

        return Ok(new
        {
            SaldoTotal = saldoTotal,
            TotalEntradas = totalEntradas,
            TotalSaidas = totalSaidas,
            SaldoReal = saldoReal,
            PrevisaoEntradas = previsaoEntradas,
            PrevisaoDespesas = previsaoDespesas,
            PorCategoria = porCategoria
        });
    }

    [HttpPost("extrair-anexo")]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> ExtrairAnexo(IFormFile ficheiro)
    {
        if (ficheiro.Length == 0) return BadRequest("Ficheiro vazio.");
        var mimeType = ficheiro.ContentType;
        if (mimeType != "application/pdf" && !mimeType.StartsWith("image/"))
            return BadRequest("Só são aceites PDF ou imagens.");

        using var ms = new MemoryStream();
        await ficheiro.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());

        FaturaExtraida? dados = null;
        if (claude.Configurado)
        {
            try
            {
                dados = await claude.ExtrairFaturaAsync(mimeType, base64);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao extrair dados do anexo carregado.");
            }
        }

        var moeda = string.IsNullOrWhiteSpace(dados?.Moeda) ? "EUR" : dados.Moeda.ToUpperInvariant();
        decimal? valorConvertido = dados?.Valor;
        string? observacao = null;

        if (dados?.Valor is decimal valorOriginal && moeda != "EUR")
        {
            try
            {
                var conversao = await currency.ConverterParaEurAsync(valorOriginal, moeda);
                valorConvertido = conversao.ValorConvertido;
                observacao = currency.GerarObservacao(conversao, moeda);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao converter {Moeda} para EUR.", moeda);
            }
        }

        return Ok(new
        {
            dados?.Fornecedor,
            dados?.DataVencimento,
            Valor = valorConvertido,
            ValorOriginal = moeda != "EUR" ? dados?.Valor : null,
            Moeda = moeda,
            Observacoes = observacao,
            dados?.Entidade,
            dados?.Referencia,
            AnexoBase64 = base64,
            AnexoMimeType = mimeType
        });
    }

    [HttpPost("converter-moeda")]
    public async Task<IActionResult> ConverterMoeda([FromBody] ConverterMoedaDto dto)
    {
        try
        {
            var conversao = await currency.ConverterParaEurAsync(dto.Valor, dto.Moeda.ToUpperInvariant());
            return Ok(new
            {
                ValorConvertido = conversao.ValorConvertido,
                Observacao = currency.GerarObservacao(conversao, dto.Moeda.ToUpperInvariant())
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao converter {Moeda} para EUR.", dto.Moeda);
            return UnprocessableEntity("Não foi possível obter a cotação da moeda. Tente novamente.");
        }
    }

    [HttpGet("{id}/anexo")]
    public async Task<IActionResult> GetAnexo(int id)
    {
        var c = await db.ContasPessoais.FindAsync(id);
        if (c is null || c.AnexoBase64 is null || c.AnexoMimeType is null) return NotFound();
        return File(Convert.FromBase64String(c.AnexoBase64), c.AnexoMimeType);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ContaPessoalDto dto)
    {
        var vencimento = DateOnly.Parse(dto.DataVencimento);
        var grupo = dto.TotalRecorrencias > 1 ? (Guid?)Guid.NewGuid() : null;

        var criadas = new List<ContaPessoal>();
        for (int i = 0; i < dto.TotalRecorrencias; i++)
        {
            var dataVenc = vencimento.AddMonths(i);
            var c = new ContaPessoal
            {
                Tipo              = dto.Tipo,
                Descricao         = dto.Descricao,
                Categoria         = dto.Categoria,
                DataVencimento    = dataVenc,
                ValorPrevisto     = dto.ValorPrevisto,
                GrupoRecorrencia  = grupo,
                RecorrenciaAtual  = i + 1,
                TotalRecorrencias = dto.TotalRecorrencias,
                MesReferencia     = dataVenc.Month,
                AnoReferencia     = dataVenc.Year,
                CreatedAt         = DateTime.UtcNow,
                Entidade          = dto.Tipo == "Saida" ? dto.Entidade : null,
                Referencia        = dto.Tipo == "Saida" ? dto.Referencia : null,
                AnexoBase64       = dto.AnexoBase64,
                AnexoMimeType     = dto.AnexoMimeType,
                Moeda             = string.IsNullOrWhiteSpace(dto.Moeda) ? "EUR" : dto.Moeda,
                ValorOriginal     = dto.ValorOriginal,
                Observacoes       = dto.Observacoes,
                LembreteCalendario = dto.LembreteCalendario,
                Pago              = dto.JaPago,
                ValorPago         = dto.JaPago ? dto.ValorPrevisto : null,
                DataPagamento     = dto.JaPago && dto.DataPagamento is not null ? DateOnly.Parse(dto.DataPagamento) : null,
                MetodoPagamento   = dto.JaPago ? dto.MetodoPagamento : null
            };
            db.ContasPessoais.Add(c);
            criadas.Add(c);
        }
        await db.SaveChangesAsync();

        if (dto.LembreteCalendario && graph.Configurado)
        {
            foreach (var c in criadas)
            {
                try
                {
                    c.GraphEventId = await graph.CriarLembreteAsync(AssuntoLembrete(c), c.DataVencimento, CorpoLembrete(c));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Falha ao criar lembrete no calendário para o lançamento {Id}.", c.Id);
                }
            }
            await db.SaveChangesAsync();
        }

        return Ok(criadas.Select(ToDtoAnon));
    }

    private static string AssuntoLembrete(ContaPessoal c) =>
        $"{(c.Tipo == "Entrada" ? "Receber" : "Pagar")}: {c.Descricao} ({c.ValorPrevisto:N2} €)";

    private static string CorpoLembrete(ContaPessoal c)
    {
        var linhas = new List<string> { $"Categoria: {c.Categoria}" };
        if (c.Tipo == "Saida" && !string.IsNullOrWhiteSpace(c.Entidade)) linhas.Add($"Entidade: {c.Entidade}");
        if (c.Tipo == "Saida" && !string.IsNullOrWhiteSpace(c.Referencia)) linhas.Add($"Referência: {c.Referencia}");
        linhas.Add("Lançamento criado no MapaMensal — Financeiro.");
        return string.Join("\n", linhas);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ContaPessoalDto dto)
    {
        var c = await db.ContasPessoais.FindAsync(id);
        if (c is null) return NotFound();

        var vencimento = DateOnly.Parse(dto.DataVencimento);
        c.Tipo           = dto.Tipo;
        c.Descricao      = dto.Descricao;
        c.Categoria      = dto.Categoria;
        c.DataVencimento = vencimento;
        c.ValorPrevisto  = dto.ValorPrevisto;
        c.MesReferencia  = vencimento.Month;
        c.AnoReferencia  = vencimento.Year;
        c.Entidade       = dto.Tipo == "Saida" ? dto.Entidade : null;
        c.Referencia     = dto.Tipo == "Saida" ? dto.Referencia : null;
        c.Moeda          = string.IsNullOrWhiteSpace(dto.Moeda) ? "EUR" : dto.Moeda;
        c.ValorOriginal  = dto.ValorOriginal;
        c.Observacoes    = dto.Observacoes;
        if (dto.AnexoBase64 is not null)
        {
            c.AnexoBase64 = dto.AnexoBase64;
            c.AnexoMimeType = dto.AnexoMimeType;
        }

        if (graph.Configurado)
        {
            try
            {
                if (dto.LembreteCalendario && c.GraphEventId is null)
                {
                    c.GraphEventId = await graph.CriarLembreteAsync(AssuntoLembrete(c), c.DataVencimento, CorpoLembrete(c));
                }
                else if (dto.LembreteCalendario && c.GraphEventId is not null)
                {
                    await graph.AtualizarLembreteAsync(c.GraphEventId, AssuntoLembrete(c), c.DataVencimento, CorpoLembrete(c));
                }
                else if (!dto.LembreteCalendario && c.GraphEventId is not null)
                {
                    await graph.RemoverLembreteAsync(c.GraphEventId);
                    c.GraphEventId = null;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao sincronizar lembrete no calendário para o lançamento {Id}.", c.Id);
            }
        }
        c.LembreteCalendario = dto.LembreteCalendario;

        await db.SaveChangesAsync();
        return Ok(ToDtoAnon(c));
    }

    [HttpPatch("{id}/pagar")]
    public async Task<IActionResult> Pagar(int id, [FromBody] PagarDto dto)
    {
        var c = await db.ContasPessoais.FindAsync(id);
        if (c is null) return NotFound();

        c.Pago            = dto.Pago;
        c.ValorPago       = dto.Pago ? dto.ValorPago : null;
        c.DataPagamento   = dto.Pago && dto.DataPagamento is not null
            ? DateOnly.Parse(dto.DataPagamento)
            : null;
        c.MetodoPagamento = dto.Pago ? dto.MetodoPagamento : null;

        var faturaCartao = await db.FaturasCartao.FirstOrDefaultAsync(f => f.ContaPessoalId == c.Id);
        if (faturaCartao is not null)
        {
            if (!dto.Pago)
            {
                faturaCartao.PagamentoStatus = "NaoPago";
                faturaCartao.ValorPagoEur = null;
            }
            else
            {
                var valorPago = dto.ValorPago ?? 0;
                faturaCartao.ValorPagoEur = valorPago;
                faturaCartao.PagamentoStatus = valorPago >= c.ValorPrevisto ? "Pago" : "Parcial";
            }
        }

        await db.SaveChangesAsync();
        return Ok(ToDtoAnon(c));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool grupo = false)
    {
        var c = await db.ContasPessoais.FindAsync(id);
        if (c is null) return NotFound();

        var paraRemover = new List<ContaPessoal> { c };
        if (grupo && c.GrupoRecorrencia.HasValue)
        {
            paraRemover = await db.ContasPessoais
                .Where(x => x.GrupoRecorrencia == c.GrupoRecorrencia && !x.Pago)
                .ToListAsync();
        }

        if (graph.Configurado)
        {
            foreach (var item in paraRemover.Where(x => x.GraphEventId is not null))
            {
                try { await graph.RemoverLembreteAsync(item.GraphEventId!); }
                catch (Exception ex) { logger.LogWarning(ex, "Falha ao remover lembrete do calendário para o lançamento {Id}.", item.Id); }
            }
        }

        db.ContasPessoais.RemoveRange(paraRemover);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static object ToDtoAnon(ContaPessoal c) => new
    {
        c.Id, c.Tipo, c.Descricao, c.Categoria,
        DataVencimento   = c.DataVencimento.ToString("yyyy-MM-dd"),
        DataPagamento    = c.DataPagamento?.ToString("yyyy-MM-dd"),
        c.ValorPrevisto, c.ValorPago, c.Pago, c.MetodoPagamento,
        GrupoRecorrencia = c.GrupoRecorrencia?.ToString(),
        c.RecorrenciaAtual, c.TotalRecorrencias,
        c.MesReferencia, c.AnoReferencia, c.TimesheetFaturaId,
        c.Entidade, c.Referencia,
        TemAnexo = c.AnexoBase64 != null,
        c.AnexoMimeType,
        c.Moeda, c.ValorOriginal, c.Observacoes,
        c.LembreteCalendario,
        CreatedAt = c.CreatedAt.ToString("yyyy-MM-dd")
    };
}

public record ContaPessoalDto(
    string Tipo, string Descricao, string Categoria, string DataVencimento,
    decimal ValorPrevisto, int TotalRecorrencias,
    string? Entidade = null, string? Referencia = null,
    string? AnexoBase64 = null, string? AnexoMimeType = null,
    string? Moeda = null, decimal? ValorOriginal = null, string? Observacoes = null,
    bool LembreteCalendario = false,
    bool JaPago = false, string? DataPagamento = null, string? MetodoPagamento = null
);

public record PagarDto(bool Pago, decimal? ValorPago, string? DataPagamento, string? MetodoPagamento = null);

public record ConverterMoedaDto(decimal Valor, string Moeda);
