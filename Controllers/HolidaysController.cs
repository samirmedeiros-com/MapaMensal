using Microsoft.AspNetCore.Authorization;
using MapaMensal.Data;
using MapaMensal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class HolidaysController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? year)
    {
        var query = db.Holidays.AsQueryable();
        if (year.HasValue)
        {
            var start = new DateOnly(year.Value, 1, 1);
            var end   = new DateOnly(year.Value, 12, 31);
            query = query.Where(h => h.Date >= start && h.Date <= end);
        }
        return Ok(await query.OrderBy(h => h.Date).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create(Holiday holiday)
    {
        holiday.Id = 0;
        db.Holidays.Add(holiday);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), holiday);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Holiday holiday)
    {
        if (id != holiday.Id) return BadRequest();
        db.Entry(holiday).State = EntityState.Modified;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var h = await db.Holidays.FindAsync(id);
        if (h is null) return NotFound();
        db.Holidays.Remove(h);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
