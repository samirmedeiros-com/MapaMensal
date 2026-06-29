using MapaMensal.Data;
using MapaMensal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Route("api/agenda/categorias")]
[Authorize]
public class AgendaCategoriasController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await db.CategoriasCompromisso.OrderBy(c => c.Nome).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CategoriaCompromisso dto)
    {
        var cat = new CategoriaCompromisso { Nome = dto.Nome.Trim(), Cor = dto.Cor };
        db.CategoriasCompromisso.Add(cat);
        await db.SaveChangesAsync();
        return Ok(cat);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CategoriaCompromisso dto)
    {
        var cat = await db.CategoriasCompromisso.FindAsync(id);
        if (cat is null) return NotFound();
        cat.Nome = dto.Nome.Trim();
        cat.Cor = dto.Cor;
        await db.SaveChangesAsync();
        return Ok(cat);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await db.CategoriasCompromisso.FindAsync(id);
        if (cat is null) return NotFound();
        // Desassocia compromissos desta categoria
        await db.Compromissos.Where(c => c.CategoriaId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CategoriaId, (int?)null));
        db.CategoriasCompromisso.Remove(cat);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
