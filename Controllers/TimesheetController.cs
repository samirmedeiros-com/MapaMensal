using Microsoft.AspNetCore.Authorization;
using MapaMensal.Data;
using MapaMensal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TimesheetController(AppDbContext db) : ControllerBase
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
}

public record TimesheetActionDto(int Year, int Month);
