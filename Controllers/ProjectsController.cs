using Microsoft.AspNetCore.Authorization;
using MapaMensal.Data;
using MapaMensal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProjectsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var projects = await db.Projects
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();
        return Ok(projects);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var project = await db.Projects.FindAsync(id);
        return project is null ? NotFound() : Ok(project);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Project project)
    {
        project.Id = 0;
        project.SortOrder = await db.Projects.MaxAsync(p => (int?)p.SortOrder) + 1 ?? 1;
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = project.Id }, project);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Project project)
    {
        if (id != project.Id) return BadRequest();
        db.Entry(project).State = EntityState.Modified;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var project = await db.Projects.FindAsync(id);
        if (project is null) return NotFound();
        project.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
