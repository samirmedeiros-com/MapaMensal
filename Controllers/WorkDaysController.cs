using Microsoft.AspNetCore.Authorization;
using MapaMensal.Data;
using MapaMensal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class WorkDaysController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByMonth([FromQuery] int year, [FromQuery] int month)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var days = await db.WorkDays
            .Where(w => w.Date >= from && w.Date <= to && w.Project.IsActive)
            .OrderBy(w => w.Date)
            .ThenBy(w => w.Project.SortOrder)
            .Select(w => new { w.Id, w.ProjectId, Date = w.Date.ToString("yyyy-MM-dd"), w.Mark })
            .ToListAsync();

        return Ok(days);
    }

    private async Task<bool> IsApprovedAsync(int year, int month)
    {
        return await db.TimesheetApprovals
            .AnyAsync(t => t.Year == year && t.Month == month && t.IsApproved);
    }

    private async Task<bool> IsProjectInvoicedAsync(int projectId, int year, int month)
    {
        return await db.TimesheetFaturas
            .AnyAsync(f => f.ProjectId == projectId && f.Year == year && f.Month == month);
    }

    [HttpPost("upsert")]
    public async Task<IActionResult> Upsert([FromBody] WorkDayUpsertDto dto)
    {
        if (await IsApprovedAsync(dto.Year, dto.Month))
            return Conflict("TimeSheet já aprovado. Cancele a aprovação para poder alterar.");
        if (await IsProjectInvoicedAsync(dto.ProjectId, dto.Year, dto.Month))
            return Conflict("Este projeto já tem fatura emitida neste mês, não pode ser alterado.");

        var date = new DateOnly(dto.Year, dto.Month, dto.Day);
        var existing = await db.WorkDays
            .FirstOrDefaultAsync(w => w.ProjectId == dto.ProjectId && w.Date == date);

        if (existing is null)
        {
            db.WorkDays.Add(new WorkDay
            {
                ProjectId = dto.ProjectId,
                Date = date,
                Mark = dto.Mark
            });
        }
        else
        {
            existing.Mark = dto.Mark;
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkUpsert([FromBody] List<WorkDayUpsertDto> dtos)
    {
        var meses = dtos.Select(d => (d.Year, d.Month)).Distinct();
        foreach (var (year, month) in meses)
        {
            if (await IsApprovedAsync(year, month))
                return Conflict("TimeSheet já aprovado. Cancele a aprovação para poder alterar.");
        }

        var projetosMes = dtos.Select(d => (d.ProjectId, d.Year, d.Month)).Distinct();
        foreach (var (projectId, year, month) in projetosMes)
        {
            if (await IsProjectInvoicedAsync(projectId, year, month))
                return Conflict("Este projeto já tem fatura emitida neste mês, não pode ser alterado.");
        }

        foreach (var dto in dtos)
        {
            var date = new DateOnly(dto.Year, dto.Month, dto.Day);
            var existing = await db.WorkDays
                .FirstOrDefaultAsync(w => w.ProjectId == dto.ProjectId && w.Date == date);

            if (existing is null)
                db.WorkDays.Add(new WorkDay { ProjectId = dto.ProjectId, Date = date, Mark = dto.Mark });
            else
                existing.Mark = dto.Mark;
        }
        await db.SaveChangesAsync();
        return Ok();
    }
}

public record WorkDayUpsertDto(int ProjectId, int Year, int Month, int Day, decimal Mark);
