using MapaMensal.Data;
using MapaMensal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Route("api/categorias-contas-pessoais")]
[Authorize]
public class CategoriasContasPessoaisController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.CategoriasContasPessoais.OrderBy(c => c.Ordem).ThenBy(c => c.Nome).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CategoriaContaPessoal cat)
    {
        cat.Id = 0;
        if (string.IsNullOrWhiteSpace(cat.Nome)) return BadRequest("Nome obrigatório");
        db.CategoriasContasPessoais.Add(cat);
        await db.SaveChangesAsync();
        return Ok(cat);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CategoriaContaPessoal cat)
    {
        if (id != cat.Id) return BadRequest();
        db.Entry(cat).State = EntityState.Modified;
        await db.SaveChangesAsync();
        return Ok(cat);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await db.CategoriasContasPessoais.FindAsync(id);
        if (cat == null) return NotFound();
        db.CategoriasContasPessoais.Remove(cat);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
