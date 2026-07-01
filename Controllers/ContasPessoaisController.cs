using MapaMensal.Data;
using MapaMensal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContasPessoaisController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int year, [FromQuery] int? month)
    {
        IQueryable<ContaPessoal> query;
        if (month.HasValue)
            query = db.ContasPessoais.Where(c => c.AnoReferencia == year && c.MesReferencia == month.Value);
        else
            query = db.ContasPessoais.Where(c => c.AnoReferencia == year);

        var result = await query
            .OrderBy(c => c.DataVencimento)
            .ThenBy(c => c.Categoria)
            .Select(c => new
            {
                c.Id, c.Descricao, c.Categoria,
                DataVencimento  = c.DataVencimento.ToString("yyyy-MM-dd"),
                DataPagamento   = c.DataPagamento.HasValue ? c.DataPagamento.Value.ToString("yyyy-MM-dd") : null,
                c.ValorPrevisto, c.ValorPago, c.Pago, c.MetodoPagamento,
                GrupoRecorrencia = c.GrupoRecorrencia.HasValue ? c.GrupoRecorrencia.Value.ToString() : null,
                c.RecorrenciaAtual, c.TotalRecorrencias,
                CreatedAt = c.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(result);
    }

    [HttpGet("resumo-anual")]
    public async Task<IActionResult> ResumoAnual([FromQuery] int year)
    {
        var anoStart = new DateOnly(year, 1, 1);
        var anoEnd   = new DateOnly(year, 12, 31);
        var contas = await db.ContasPessoais
            .Where(c => c.DataVencimento >= anoStart && c.DataVencimento <= anoEnd)
            .ToListAsync();

        var porMes = Enumerable.Range(1, 12).Select(m => new
        {
            Mes = m,
            Previsto = contas.Where(c => c.DataVencimento.Month == m).Sum(c => c.ValorPrevisto),
            Pago     = contas.Where(c => c.DataVencimento.Month == m && c.Pago).Sum(c => c.ValorPago ?? 0)
        });

        var porCategoria = contas
            .GroupBy(c => c.Categoria)
            .Select(g => new { Categoria = g.Key, Total = g.Sum(c => c.ValorPrevisto) })
            .OrderByDescending(x => x.Total);

        return Ok(new { PorMes = porMes, PorCategoria = porCategoria });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ContaPessoalDto dto)
    {
        var vencimento = DateOnly.Parse(dto.DataVencimento);
        var grupo = dto.TotalRecorrencias > 1 ? (Guid?)Guid.NewGuid() : null;
        var mesRef = dto.MesReferencia ?? vencimento.Month;
        var anoRef = dto.AnoReferencia ?? vencimento.Year;

        var criadas = new List<ContaPessoal>();
        for (int i = 0; i < dto.TotalRecorrencias; i++)
        {
            var refDate = new DateOnly(anoRef, mesRef, 1).AddMonths(i);
            var c = new ContaPessoal
            {
                Descricao         = dto.Descricao,
                Categoria         = dto.Categoria,
                DataVencimento    = vencimento.AddMonths(i),
                ValorPrevisto     = dto.ValorPrevisto,
                GrupoRecorrencia  = grupo,
                RecorrenciaAtual  = i + 1,
                TotalRecorrencias = dto.TotalRecorrencias,
                MesReferencia     = refDate.Month,
                AnoReferencia     = refDate.Year,
                CreatedAt         = DateTime.UtcNow
            };
            db.ContasPessoais.Add(c);
            criadas.Add(c);
        }
        await db.SaveChangesAsync();
        return Ok(criadas.Select(ToDto));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ContaPessoalDto dto)
    {
        var c = await db.ContasPessoais.FindAsync(id);
        if (c is null) return NotFound();

        c.Descricao      = dto.Descricao;
        c.Categoria      = dto.Categoria;
        c.DataVencimento = DateOnly.Parse(dto.DataVencimento);
        c.ValorPrevisto  = dto.ValorPrevisto;
        await db.SaveChangesAsync();
        return Ok(ToDto(c));
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
        await db.SaveChangesAsync();
        return Ok(ToDto(c));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool grupo = false)
    {
        var c = await db.ContasPessoais.FindAsync(id);
        if (c is null) return NotFound();

        if (grupo && c.GrupoRecorrencia.HasValue)
        {
            var todas = await db.ContasPessoais
                .Where(x => x.GrupoRecorrencia == c.GrupoRecorrencia && !x.Pago)
                .ToListAsync();
            db.ContasPessoais.RemoveRange(todas);
        }
        else
        {
            db.ContasPessoais.Remove(c);
        }
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static object ToDto(ContaPessoal c) => new
    {
        c.Id, c.Descricao, c.Categoria,
        DataVencimento   = c.DataVencimento.ToString("yyyy-MM-dd"),
        DataPagamento    = c.DataPagamento?.ToString("yyyy-MM-dd"),
        c.ValorPrevisto, c.ValorPago, c.Pago, c.MetodoPagamento,
        GrupoRecorrencia = c.GrupoRecorrencia?.ToString(),
        c.RecorrenciaAtual, c.TotalRecorrencias,
        c.MesReferencia, c.AnoReferencia,
        CreatedAt = c.CreatedAt.ToString("yyyy-MM-dd")
    };
}

public record ContaPessoalDto(
    string Descricao, string Categoria, string DataVencimento,
    decimal ValorPrevisto, int TotalRecorrencias,
    int? MesReferencia = null, int? AnoReferencia = null
);

public record PagarDto(bool Pago, decimal? ValorPago, string? DataPagamento, string? MetodoPagamento = null);
