using Microsoft.AspNetCore.Authorization;
using MapaMensal.Data;
using MapaMensal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ExpensesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? year)
    {
        var query = db.Expenses.AsQueryable();
        if (year.HasValue)
            query = query.Where(e => e.Year == year.Value);
        return Ok(await query.OrderBy(e => e.Year).ThenBy(e => e.Month).ThenBy(e => e.Category).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Expense expense)
    {
        expense.Id = 0;
        db.Expenses.Add(expense);
        await db.SaveChangesAsync();
        return Ok(expense);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Expense expense)
    {
        if (id != expense.Id) return BadRequest();
        db.Entry(expense).State = EntityState.Modified;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await db.Expenses.FindAsync(id);
        if (e is null) return NotFound();
        db.Expenses.Remove(e);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
