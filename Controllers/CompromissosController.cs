using MapaMensal.Data;
using MapaMensal.Models;
using MapaMensal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MapaMensal.Helpers;

namespace MapaMensal.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompromissosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly string _baseUrl;

    public CompromissosController(AppDbContext db, IEmailService email, IConfiguration config)
    {
        _db = db;
        _email = email;
        _baseUrl = config["App:BaseUrl"] ?? "https://restpje.azurewebsites.net";
    }

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
        => Ok(await _db.HorariosDisponiveis.OrderBy(h => h.DiaSemana).ThenBy(h => h.HoraInicio).ToListAsync());

    [HttpPut("horarios")]
    public async Task<IActionResult> SaveHorarios([FromBody] List<HorarioDisponivel> horarios)
    {
        var existentes = await _db.HorariosDisponiveis.ToListAsync();
        _db.HorariosDisponiveis.RemoveRange(existentes);
        _db.HorariosDisponiveis.AddRange(horarios.Select(h => new HorarioDisponivel
        {
            DiaSemana = h.DiaSemana,
            HoraInicio = h.HoraInicio,
            HoraFim = h.HoraFim,
            DuracaoSlotMinutos = h.DuracaoSlotMinutos,
            Ativo = h.Ativo
        }));
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── CRUD compromissos ─────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? ano, [FromQuery] int? mes)
    {
        var q = _db.Compromissos
            .Include(c => c.Participantes)
            .Include(c => c.Project)
            .AsQueryable();

        if (ano.HasValue) q = q.Where(c => c.Inicio.Year == ano.Value);
        if (mes.HasValue) q = q.Where(c => c.Inicio.Month == mes.Value);

        return Ok(await q.OrderBy(c => c.Inicio).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await _db.Compromissos
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
                Notificar = p.Notificar,
                Token = Guid.NewGuid().ToString("N")
            }).ToList()
        };

        _db.Compromissos.Add(compromisso);
        await _db.SaveChangesAsync();

        if (dto.NotificarParticipantes)
        {
            try
            {
                var ics = IcsHelper.Gerar(compromisso.Titulo, compromisso.Descricao,
                    compromisso.Inicio, compromisso.Fim, compromisso.Local, compromisso.Id.ToString());

                await Task.WhenAll(compromisso.Participantes
                    .Where(p => p.Notificar && !string.IsNullOrEmpty(p.Email))
                    .Select(p => _email.SendConviteCompromissoAsync(
                        p.Email, p.Nome, compromisso.Titulo,
                        compromisso.Inicio, compromisso.Fim,
                        compromisso.Local, compromisso.Descricao, ics,
                        _baseUrl + "/api/compromissos/aceitar/" + p.Token)));
            }
            catch { /* não falha a criação por erro de email */ }
        }

        return CreatedAtAction(nameof(GetById), new { id = compromisso.Id }, compromisso);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CompromissoCreateDto dto)
    {
        var compromisso = await _db.Compromissos
            .Include(c => c.Participantes)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (compromisso is null) return NotFound();

        var dadosAntigos = compromisso.Participantes
            .ToDictionary(p => p.Email, p => (p.Token, p.Aceite, p.AceiteEm));

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

        _db.CompromissoParticipantes.RemoveRange(compromisso.Participantes.ToList());
        compromisso.Participantes = dto.Participantes.Select(p =>
        {
            var antigo = dadosAntigos.GetValueOrDefault(p.Email);
            return new CompromissoParticipante
            {
                CompromissoId = id,
                Nome = p.Nome,
                Email = p.Email,
                Telefone = p.Telefone,
                CodigoPais = p.CodigoPais,
                Notificar = p.Notificar,
                Token = antigo.Token ?? Guid.NewGuid().ToString("N"),
                Aceite = antigo.Aceite,
                AceiteEm = antigo.AceiteEm
            };
        }).ToList();

        await _db.SaveChangesAsync();

        if (dto.NotificarParticipantes)
        {
            try
            {
                var ics = IcsHelper.Gerar(compromisso.Titulo, compromisso.Descricao,
                    compromisso.Inicio, compromisso.Fim, compromisso.Local, id.ToString());

                await Task.WhenAll(compromisso.Participantes
                    .Where(p => p.Notificar && !string.IsNullOrEmpty(p.Email))
                    .Select(p => _email.SendConviteAlteradoAsync(
                        p.Email, p.Nome, compromisso.Titulo,
                        compromisso.Inicio, compromisso.Fim,
                        compromisso.Local, compromisso.Descricao, ics,
                        _baseUrl + "/api/compromissos/aceitar/" + p.Token)));
            }
            catch { /* não falha a actualização por erro de email */ }
        }

        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] CompromissoUpdateStatusDto dto)
    {
        var c = await _db.Compromissos.FindAsync(id);
        if (c is null) return NotFound();
        c.Status = dto.Status;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Compromissos.FindAsync(id);
        if (c is null) return NotFound();
        _db.Compromissos.Remove(c);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/reenviar-email")]
    public async Task<IActionResult> ReenviarEmail(int id)
    {
        var compromisso = await _db.Compromissos
            .Include(c => c.Participantes)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (compromisso is null) return NotFound();

        try
        {
            var ics = IcsHelper.Gerar(compromisso.Titulo, compromisso.Descricao,
                compromisso.Inicio, compromisso.Fim, compromisso.Local, id.ToString());

            await Task.WhenAll(compromisso.Participantes
                .Where(p => p.Notificar && !string.IsNullOrEmpty(p.Email))
                .Select(p => _email.SendConviteCompromissoAsync(
                    p.Email, p.Nome, compromisso.Titulo,
                    compromisso.Inicio, compromisso.Fim,
                    compromisso.Local, compromisso.Descricao, ics,
                    _baseUrl + "/api/compromissos/aceitar/" + p.Token)));
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Erro ao reenviar email: " + ex.Message);
        }

        return NoContent();
    }
}
