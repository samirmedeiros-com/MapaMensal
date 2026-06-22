using Microsoft.AspNetCore.Authorization;
using MapaMensal.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SummaryController(AppDbContext db) : ControllerBase
{
    [HttpGet("annual")]
    public async Task<IActionResult> Annual([FromQuery] int year)
    {
        var ivaRateStr = await db.AppConfigs
            .Where(c => c.Key == "IvaRate")
            .Select(c => c.Value)
            .FirstOrDefaultAsync() ?? "0.23";
        var ivaRate = decimal.Parse(ivaRateStr, System.Globalization.CultureInfo.InvariantCulture);

        var projects = await db.Projects.Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();

        var workDays = await db.WorkDays
            .Include(w => w.Project)
            .Where(w => w.Date.Year == year && w.Project.IsActive)
            .ToListAsync();

        var projectSummaries = projects.Select(p =>
        {
            var pDays = workDays.Where(w => w.ProjectId == p.Id).ToList();
            var workedDays = pDays.Where(w => w.Mark > 0).Sum(w => w.Mark);
            var vacationDays = pDays.Count(w => w.Mark == -1);
            var valueNoIva = workedDays * p.DailyRate;
            var iva = valueNoIva * ivaRate;
            return new
            {
                p.Id,
                p.Name,
                DailyRate = p.DailyRate,
                WorkedDays = workedDays,
                VacationDays = vacationDays,
                ValueNoIva = valueNoIva,
                Iva = iva,
                TotalWithIva = valueNoIva + iva
            };
        }).ToList();

        var months = workDays.Select(w => w.Date.Month).Distinct().OrderBy(m => m).ToList();
        var monthlyDetail = months.Select(m =>
        {
            var mDays = workDays.Where(w => w.Date.Month == m).ToList();
            var perProject = projects.Select(p =>
            {
                var pDays = mDays.Where(w => w.ProjectId == p.Id).ToList();
                var worked = pDays.Where(w => w.Mark > 0).Sum(w => w.Mark);
                return new { p.Id, p.Name, Days = worked, Value = worked * p.DailyRate };
            }).ToList();
            var totalMonth = perProject.Sum(x => x.Value);
            return new { Month = m, Projects = perProject, TotalMonth = totalMonth };
        }).ToList();

        return Ok(new
        {
            Year = year,
            IvaRate = ivaRate,
            Projects = projectSummaries,
            Totals = new
            {
                WorkedDays = projectSummaries.Sum(p => p.WorkedDays),
                ValueNoIva = projectSummaries.Sum(p => p.ValueNoIva),
                Iva = projectSummaries.Sum(p => p.Iva),
                TotalWithIva = projectSummaries.Sum(p => p.TotalWithIva)
            },
            MonthlyDetail = monthlyDetail
        });
    }

    [HttpGet("treasury")]
    public async Task<IActionResult> Treasury([FromQuery] int year)
    {
        var ivaRateStr = await db.AppConfigs
            .Where(c => c.Key == "IvaRate")
            .Select(c => c.Value)
            .FirstOrDefaultAsync() ?? "0.23";
        var ivaRate = decimal.Parse(ivaRateStr, System.Globalization.CultureInfo.InvariantCulture);

        var projects = await db.Projects.Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();
        var workDays = await db.WorkDays
            .Include(w => w.Project)
            .Where(w => w.Date.Year == year && w.Project.IsActive)
            .ToListAsync();
        var expenses = await db.Expenses.Where(e => e.Year == year).ToListAsync();

        var months = Enumerable.Range(1, 12).ToList();

        var monthlyRows = months.Select(m =>
        {
            var mDays = workDays.Where(w => w.Date.Month == m).ToList();
            var receivables = projects.Select(p =>
            {
                var worked = mDays.Where(w => w.ProjectId == p.Id && w.Mark > 0).Sum(w => w.Mark);
                var noIva = worked * p.DailyRate;
                return new { p.Id, p.Name, NoIva = noIva, WithIva = noIva * (1 + ivaRate) };
            }).ToList();

            var totalReceivableNoIva = receivables.Sum(r => r.NoIva);
            var totalReceivableWithIva = receivables.Sum(r => r.WithIva);
            var ivaCollected = totalReceivableWithIva - totalReceivableNoIva;

            var mExpenses = expenses.Where(e => e.Month == m).ToList();
            var expenseSubtotal = mExpenses.Sum(e => e.Amount);
            var totalPayable = expenseSubtotal + ivaCollected;

            return new
            {
                Month = m,
                Receivables = receivables,
                TotalReceivableNoIva = totalReceivableNoIva,
                TotalReceivableWithIva = totalReceivableWithIva,
                IvaCollected = ivaCollected,
                Expenses = mExpenses,
                ExpenseSubtotal = expenseSubtotal,
                TotalPayable = totalPayable,
                Balance = totalReceivableNoIva - expenseSubtotal
            };
        }).ToList();

        decimal accumulated = 0;
        var withAccumulated = monthlyRows.Select(r =>
        {
            accumulated += r.Balance;
            return new
            {
                r.Month,
                r.Receivables,
                r.TotalReceivableNoIva,
                r.TotalReceivableWithIva,
                r.IvaCollected,
                r.Expenses,
                r.ExpenseSubtotal,
                r.TotalPayable,
                r.Balance,
                AccumulatedBalance = accumulated
            };
        }).ToList();

        return Ok(new { Year = year, Months = withAccumulated });
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var configs = await db.AppConfigs.ToListAsync();
        return Ok(configs.ToDictionary(c => c.Key, c => c.Value));
    }

    [HttpPost("config")]
    public async Task<IActionResult> SetConfig([FromBody] Dictionary<string, string> values)
    {
        foreach (var (key, value) in values)
        {
            var config = await db.AppConfigs.FirstOrDefaultAsync(c => c.Key == key);
            if (config is null)
                db.AppConfigs.Add(new() { Key = key, Value = value });
            else
                config.Value = value;
        }
        await db.SaveChangesAsync();
        return Ok();
    }
}
