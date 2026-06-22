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

    [HttpPost("upsert")]
    public async Task<IActionResult> Upsert([FromBody] WorkDayUpsertDto dto)
    {
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
