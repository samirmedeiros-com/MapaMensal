using MapaMensal.Data;
using MapaMensal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TarefasController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? projectId, [FromQuery] string? status, [FromQuery] bool arquivado = false)
    {
        var query = db.Tarefas.Where(t => t.Arquivado == arquivado);
        if (projectId.HasValue)
            query = query.Where(t => t.ProjectId == projectId.Value);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        var result = await query
            .OrderBy(t => t.DataEntrega)
            .ThenBy(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id, t.ProjectId,
                ProjectName = t.Project.Name,
                t.Titulo, t.Descricao, t.Status,
                CreatedAt = t.CreatedAt.ToString("yyyy-MM-dd"),
                DataEntrega = t.DataEntrega.HasValue ? t.DataEntrega.Value.ToString("yyyy-MM-dd") : null,
                t.HorasGastas, t.Arquivado,
                NumComentarios = t.Comentarios.Count()
            })
            .ToListAsync();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TarefaDto dto)
    {
        var tarefa = new Tarefa
        {
            ProjectId = dto.ProjectId,
            Titulo = dto.Titulo,
            Descricao = dto.Descricao,
            Status = dto.Status ?? "Backlog",
            CreatedAt = DateTime.UtcNow,
            DataEntrega = dto.DataEntrega is not null ? DateOnly.Parse(dto.DataEntrega) : null,
            HorasGastas = dto.HorasGastas
        };
        db.Tarefas.Add(tarefa);
        await db.SaveChangesAsync();
        await db.Entry(tarefa).Reference(t => t.Project).LoadAsync();
        return Ok(ToDto(tarefa));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TarefaDto dto)
    {
        var tarefa = await db.Tarefas.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == id);
        if (tarefa is null) return NotFound();

        tarefa.ProjectId = dto.ProjectId;
        tarefa.Titulo = dto.Titulo;
        tarefa.Descricao = dto.Descricao;
        tarefa.Status = dto.Status ?? tarefa.Status;
        tarefa.DataEntrega = dto.DataEntrega is not null ? DateOnly.Parse(dto.DataEntrega) : null;
        tarefa.HorasGastas = dto.HorasGastas;
        await db.SaveChangesAsync();
        return Ok(ToDto(tarefa));
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusDto dto)
    {
        var tarefa = await db.Tarefas.FindAsync(id);
        if (tarefa is null) return NotFound();
        tarefa.Status = dto.Status;
        await db.SaveChangesAsync();
        return Ok(new { tarefa.Id, tarefa.Status, tarefa.Arquivado });
    }

    [HttpPatch("{id}/arquivar")]
    public async Task<IActionResult> Arquivar(int id)
    {
        var tarefa = await db.Tarefas.FindAsync(id);
        if (tarefa is null) return NotFound();
        if (tarefa.Status != "Concluido")
            return BadRequest(new { message = "Apenas tarefas concluídas podem ser arquivadas." });
        tarefa.Arquivado = true;
        await db.SaveChangesAsync();
        return Ok(new { tarefa.Id, tarefa.Arquivado });
    }

    [HttpPatch("{id}/desarquivar")]
    public async Task<IActionResult> Desarquivar(int id)
    {
        var tarefa = await db.Tarefas.FindAsync(id);
        if (tarefa is null) return NotFound();
        tarefa.Arquivado = false;
        await db.SaveChangesAsync();
        return Ok(new { tarefa.Id, tarefa.Arquivado });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var tarefa = await db.Tarefas.FindAsync(id);
        if (tarefa is null) return NotFound();
        db.Tarefas.Remove(tarefa);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Comentários ───────────────────────────────────────────────────────────

    [HttpGet("{id}/comentarios")]
    public async Task<IActionResult> GetComentarios(int id)
    {
        if (!await db.Tarefas.AnyAsync(t => t.Id == id)) return NotFound();

        var comentarios = await db.TarefaComentarios
            .Where(c => c.TarefaId == id)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new { c.Id, c.Texto, c.Autor, CreatedAt = c.CreatedAt })
            .ToListAsync();

        return Ok(comentarios);
    }

    [HttpPost("{id}/comentarios")]
    public async Task<IActionResult> AddComentario(int id, [FromBody] ComentarioDto dto)
    {
        var texto = (dto.Texto ?? string.Empty).Trim();
        if (texto.Length == 0)
            return BadRequest(new { message = "O comentário não pode estar vazio." });
        if (!await db.Tarefas.AnyAsync(t => t.Id == id)) return NotFound();

        var comentario = new TarefaComentario
        {
            TarefaId = id,
            Texto = texto,
            Autor = User.Identity?.Name ?? "—",
            CreatedAt = DateTime.UtcNow
        };
        db.TarefaComentarios.Add(comentario);
        await db.SaveChangesAsync();

        return Ok(new { comentario.Id, comentario.Texto, comentario.Autor, comentario.CreatedAt });
    }

    [HttpDelete("comentarios/{comentarioId}")]
    public async Task<IActionResult> DeleteComentario(int comentarioId)
    {
        var comentario = await db.TarefaComentarios.FindAsync(comentarioId);
        if (comentario is null) return NotFound();

        // Só o autor apaga o próprio comentário; um Admin apaga qualquer um.
        var euSou = User.Identity?.Name;
        if (comentario.Autor != euSou && !User.IsInRole("Admin"))
            return Forbid();

        db.TarefaComentarios.Remove(comentario);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static object ToDto(Tarefa t) => new
    {
        t.Id, t.ProjectId,
        ProjectName = t.Project.Name,
        t.Titulo, t.Descricao, t.Status,
        CreatedAt = t.CreatedAt.ToString("yyyy-MM-dd"),
        DataEntrega = t.DataEntrega?.ToString("yyyy-MM-dd"),
        t.HorasGastas, t.Arquivado,
        NumComentarios = t.Comentarios.Count
    };
}

public record TarefaDto(int ProjectId, string Titulo, string? Descricao, string? Status, string? DataEntrega, decimal HorasGastas);
public record StatusDto(string Status);
public record ComentarioDto(string Texto);
