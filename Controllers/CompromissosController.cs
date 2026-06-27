using MapaMensal.Data;
using MapaMensal.Helpers;
using MapaMensal.Models;
using MapaMensal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompromissosController(AppDbContext db, IEmailService email) : ControllerBase
{
    // ── DTOs ──────────────────────────────────────────────────────────────

    public record ParticipanteDto(string Nome, string Email, string? Telefone, string? CodigoPais, bool Notificar);

    public record CompromissoCreateDto(
        string Titulo, string? Descricao,
        DateTime Inicio, DateTime Fim,
        int? ProjectId, int? ContaPessoalId,
        string Local, bool Online, string? LinkOnline,
        TipoCompromisso Tipo,
        bool NotificarParticipantes,
        List<ParticipanteDto> Participantes);

    public record CompromissoUpdateStatusDto(StatusCompromisso Status);

    // ── Horários disponíveis ───────────────────────────────────────────────

    [HttpGet("horarios")]
    public async Task<IActionResult> GetHorarios()
        => Ok(await db.HorariosDisponiveis.OrderBy(h => h.DiaSemana).ThenBy(h => h.HoraInicio).ToListAsync());

    [HttpPut("horarios")]
    public async Task<IActionResult> SaveHorarios([FromBody] List<HorarioDisponivel> horarios)
    {
        var existentes = await db.HorariosDisponiveis.ToListAsync();
        db.HorariosDisponiveis.RemoveRange(existentes);
        db.HorariosDisponiveis.AddRange(horarios.Select(h => new HorarioDisponivel
        {
            DiaSemana = h.DiaSemana,
            HoraInicio = h.HoraInicio,
            HoraFim = h.HoraFim,
            DuracaoSlotMinutos = h.DuracaoSlotMinutos,
            Ativo = h.Ativo
        }));
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── CRUD compromissos ─────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? ano, [FromQuery] int? mes)
    {
        var q = db.Compromissos
            .Include(c => c.Participantes)
            .Include(c => c.Project)
            .AsQueryable();

        if (ano.HasValue)
            q = q.Where(c => c.Inicio.Year == ano.Value);
        if (mes.HasValue)
            q = q.Where(c => c.Inicio.Month == mes.Value);

        var lista = await q.OrderBy(c => c.Inicio).ToListAsync();
        return Ok(lista);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await db.Compromissos
            .Include(c => c.Participantes)
            .Include(c => c.Project)
            .FirstOrDefaultAsync(c => c.Id == id);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CompromissoCreateDto dto)
    {
        var compromisso = new Compromisso
        {
            Titulo = dto.Titulo,
            Descricao = dto.Descricao,
            Inicio = dto.Inicio,
            Fim = dto.Fim,
            ProjectId = dto.ProjectId,
            ContaPessoalId = dto.ContaPessoalId,
            Local = dto.Local,
            Online = dto.Online,
            LinkOnline = dto.LinkOnline,
            Tipo = dto.Tipo,
            NotificarParticipantes = dto.NotificarParticipantes,
            Status = StatusCompromisso.Agendado,
            CriadoEm = DateTime.UtcNow,
            Participantes = dto.Participantes.Select(p => new CompromissoParticipante
            {
                Nome = p.Nome,
                Email = p.Email,
                Telefone = p.Telefone,
                CodigoPais = p.CodigoPais,
                Notificar = p.Notificar
            }).ToList()
        };

        db.Compromissos.Add(compromisso);
        await db.SaveChangesAsync();

        // Enviar notificações aos participantes que pediram
        if (dto.NotificarParticipantes)
        {
            try
            {
                var ics = IcsHelper.Gerar(
                    compromisso.Titulo, compromisso.Descricao,
                    compromisso.Inicio, compromisso.Fim,
                    compromisso.Local, compromisso.Id.ToString());

                var tarefasEmail = compromisso.Participantes
                    .Where(p => p.Notificar && !string.IsNullOrEmpty(p.Email))
                    .Select(p => email.SendConviteCompromissoAsync(
                        p.Email, p.Nome, compromisso.Titulo,
                        compromisso.Inicio, compromisso.Fim,
                        compromisso.Local, compromisso.Descricao, ics));

                await Task.WhenAll(tarefasEmail);
            }
            catch { /* não falha a criação por erro de email */ }
        }

        return CreatedAtAction(nameof(GetById), new { id = compromisso.Id }, compromisso);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CompromissoCreateDto dto)
    {
        var compromisso = await db.Compromissos
            .Include(c => c.Participantes)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (compromisso is null) return NotFound();

        var participantesAntigos = compromisso.Participantes.ToList();

        compromisso.Titulo = dto.Titulo;
        compromisso.Descricao = dto.Descricao;
        compromisso.Inicio = dto.Inicio;
        compromisso.Fim = dto.Fim;
        compromisso.ProjectId = dto.ProjectId;
        compromisso.ContaPessoalId = dto.ContaPessoalId;
        compromisso.Local = dto.Local;
        compromisso.Online = dto.Online;
        compromisso.LinkOnline = dto.LinkOnline;
        compromisso.Tipo = dto.Tipo;
        compromisso.NotificarParticipantes = dto.NotificarParticipantes;

        db.CompromissoParticipantes.RemoveRange(participantesAntigos);
        compromisso.Participantes = dto.Participantes.Select(p => new CompromissoParticipante
        {
            CompromissoId = id,
            Nome = p.Nome,
            Email = p.Email,
            Telefone = p.Telefone,
            CodigoPais = p.CodigoPais,
            Notificar = p.Notificar
        }).ToList();

        await db.SaveChangesAsync();

        // Notificar novos participantes
        if (dto.NotificarParticipantes)
        {
            try
            {
                var ics = IcsHelper.Gerar(
                    compromisso.Titulo, compromisso.Descricao,
                    compromisso.Inicio, compromisso.Fim,
                    compromisso.Local, id.ToString());

                var emailsAntigos = participantesAntigos.Select(p => p.Email).ToHashSet();
                var novos = compromisso.Participantes
                    .Where(p => p.Notificar && !string.IsNullOrEmpty(p.Email) && !emailsAntigos.Contains(p.Email));

                await Task.WhenAll(novos.Select(p => email.SendConviteCompromissoAsync(
                    p.Email, p.Nome, compromisso.Titulo,
                    compromisso.Inicio, compromisso.Fim,
                    compromisso.Local, compromisso.Descricao, ics)));
            }
            catch { /* não falha a actualização por erro de email */ }
        }

        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] CompromissoUpdateStatusDto dto)
    {
        var c = await db.Compromissos.FindAsync(id);
        if (c is null) return NotFound();
        c.Status = dto.Status;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await db.Compromissos.FindAsync(id);
        if (c is null) return NotFound();
        db.Compromissos.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
