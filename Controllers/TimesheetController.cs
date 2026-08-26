using Microsoft.AspNetCore.Authorization;
using MapaMensal.Data;
using MapaMensal.Models;
using MapaMensal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TimesheetController(AppDbContext db, ITocOnlineInvoiceService invoiceService) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] int year, [FromQuery] int month)
    {
        var approval = await db.TimesheetApprovals
            .FirstOrDefaultAsync(t => t.Year == year && t.Month == month);

        return Ok(new
        {
            IsApproved = approval?.IsApproved ?? false,
            ApprovedAt = approval?.ApprovedAt,
            ApprovedByUsername = approval?.ApprovedByUsername
        });
    }

    [HttpPost("aprovar")]
    public async Task<IActionResult> Aprovar([FromBody] TimesheetActionDto dto)
    {
        var approval = await db.TimesheetApprovals
            .FirstOrDefaultAsync(t => t.Year == dto.Year && t.Month == dto.Month);

        if (approval is null)
        {
            approval = new TimesheetApproval { Year = dto.Year, Month = dto.Month };
            db.TimesheetApprovals.Add(approval);
        }

        approval.IsApproved = true;
        approval.ApprovedAt = DateTime.UtcNow;
        approval.ApprovedByUsername = User.Identity?.Name;

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("cancelar-aprovacao")]
    public async Task<IActionResult> CancelarAprovacao([FromBody] TimesheetActionDto dto)
    {
        var approval = await db.TimesheetApprovals
            .FirstOrDefaultAsync(t => t.Year == dto.Year && t.Month == dto.Month);

        if (approval is not null)
        {
            approval.IsApproved = false;
            approval.ApprovedAt = null;
            approval.ApprovedByUsername = null;
            await db.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpGet("faturas")]
    public async Task<IActionResult> GetFaturas([FromQuery] int year, [FromQuery] int month)
    {
        var faturas = await db.TimesheetFaturas
            .Where(f => f.Year == year && f.Month == month)
            .Select(f => new
            {
                f.ProjectId,
                f.NumeroFatura,
                f.DataEmissao,
                f.Estado,
                f.DataRecebimento,
                TemPdf = f.PdfBase64 != null
            })
            .ToListAsync();

        return Ok(faturas);
    }

    [HttpPost("emitir-fatura")]
    public async Task<IActionResult> EmitirFatura([FromBody] EmitirFaturaDto dto)
    {
        var resultado = await invoiceService.EmitirFaturaAsync(dto.ProjectId, dto.Year, dto.Month);
        if (!resultado.Sucesso) return UnprocessableEntity(resultado.Erro);

        return Ok(new
        {
            resultado.Fatura!.NumeroFatura,
            resultado.Fatura.DataEmissao,
            resultado.Fatura.Estado
        });
    }

    [HttpGet("fatura/{projectId}/{year}/{month}/pdf")]
    public async Task<IActionResult> GetFaturaPdf(int projectId, int year, int month)
    {
        var fatura = await db.TimesheetFaturas
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Year == year && f.Month == month);

        if (fatura?.PdfBase64 is null) return NotFound();

        var bytes = Convert.FromBase64String(fatura.PdfBase64);
        return File(bytes, "application/pdf", $"Fatura_{fatura.NumeroFatura}.pdf");
    }

    [HttpPost("confirmar-recebimento")]
    public async Task<IActionResult> ConfirmarRecebimento([FromBody] EmitirFaturaDto dto)
    {
        var fatura = await db.TimesheetFaturas
            .Include(f => f.Project)
            .FirstOrDefaultAsync(f => f.ProjectId == dto.ProjectId && f.Year == dto.Year && f.Month == dto.Month);

        if (fatura is null) return NotFound("Fatura não encontrada.");
        if (fatura.Estado == "Recebida") return Ok();

        var workedDays = await db.WorkDays
            .Where(w => w.ProjectId == dto.ProjectId
                && w.Date >= new DateOnly(dto.Year, dto.Month, 1)
                && w.Date <= new DateOnly(dto.Year, dto.Month, 1).AddMonths(1).AddDays(-1)
                && w.Mark > 0)
            .SumAsync(w => w.Mark);

        var ivaRateStr = await db.AppConfigs
            .Where(c => c.Key == "IvaRate")
            .Select(c => c.Value)
            .FirstOrDefaultAsync() ?? "0.23";
        var ivaRate = decimal.Parse(ivaRateStr, System.Globalization.CultureInfo.InvariantCulture);
        var valorTotal = workedDays * fatura.Project.DailyRate;
        var valorFatura = valorTotal * (1 + ivaRate);

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        db.ContasPessoais.Add(new ContaPessoal
        {
            Tipo = "Entrada",
            Descricao = $"Fatura {fatura.NumeroFatura} — {fatura.Project.Name}",
            Categoria = "Faturação",
            DataVencimento = hoje,
            DataPagamento = hoje,
            ValorPrevisto = valorFatura,
            ValorPago = valorFatura,
            Pago = true,
            MesReferencia = dto.Month,
            AnoReferencia = dto.Year,
            TimesheetFaturaId = fatura.Id
        });

        fatura.Estado = "Recebida";
        fatura.DataRecebimento = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok();
    }
}

public record TimesheetActionDto(int Year, int Month);
public record EmitirFaturaDto(int ProjectId, int Year, int Month);
