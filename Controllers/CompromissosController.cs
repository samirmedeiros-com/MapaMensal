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
public class CompromissosController(AppDbContext db, IEmailService email, IConfiguration config) : ControllerBase
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

    private string BaseUrl => config["App:BaseUrl"] ?? "https://restpje.azurewebsites.net";

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
                Notificar = p.Notificar,
                Token = Guid.NewGuid().ToString("N")
            }).ToList()
        };

        db.Compromissos.Add(compromisso);
        await db.SaveChangesAsync();

        if (dto.NotificarParticipantes)
        {
            try
            {
                var ics = IcsHelper.Gerar(compromisso.Titulo, compromisso.Descricao,
                    compromisso.Inicio, compromisso.Fim, compromisso.Local, compromisso.Id.ToString());

                await Task.WhenAll(compromisso.Participantes
                    .Where(p => p.Notificar && !string.IsNullOrEmpty(p.Email))
                    .Select(p => email.SendConviteCompromissoAsync(
                        p.Email, p.Nome, compromisso.Titulo,
                        compromisso.Inicio, compromisso.Fim,
                        compromisso.Local, compromisso.Descricao, ics,
                        $"{BaseUrl}/api/compromissos/aceitar/{p.Token}")));
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

        // Preserva tokens e estado de aceite dos participantes existentes
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

        db.CompromissoParticipantes.RemoveRange(compromisso.Participantes.ToList());
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

        await db.SaveChangesAsync();

        // Envia email de actualização a TODOS os participantes com notificação
        if (dto.NotificarParticipantes)
        {
            try
            {
                var ics = IcsHelper.Gerar(compromisso.Titulo, compromisso.Descricao,
                    compromisso.Inicio, compromisso.Fim, compromisso.Local, id.ToString());

                await Task.WhenAll(compromisso.Participantes
                    .Where(p => p.Notificar && !string.IsNullOrEmpty(p.Email))
                    .Select(p => email.SendConviteAlteradoAsync(
                        p.Email, p.Nome, compromisso.Titulo,
                        compromisso.Inicio, compromisso.Fim,
                        compromisso.Local, compromisso.Descricao, ics,
                        $"{BaseUrl}/api/compromissos/aceitar/{p.Token}")));
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

    // ── Reenviar email ────────────────────────────────────────────────────

    [HttpPost("{id:int}/reenviar-email")]
    public async Task<IActionResult> ReenviarEmail(int id)
    {
        var compromisso = await db.Compromissos
            .Include(c => c.Participantes)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (compromisso is null) return NotFound();

        try
        {
            var ics = IcsHelper.Gerar(compromisso.Titulo, compromisso.Descricao,
                compromisso.Inicio, compromisso.Fim, compromisso.Local, id.ToString());

            await Task.WhenAll(compromisso.Participantes
                .Where(p => p.Notificar && !string.IsNullOrEmpty(p.Email))
                .Select(p => email.SendConviteCompromissoAsync(
                    p.Email, p.Nome, compromisso.Titulo,
                    compromisso.Inicio, compromisso.Fim,
                    compromisso.Local, compromisso.Descricao, ics,
                    $"{BaseUrl}/api/compromissos/aceitar/{p.Token}")));
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro ao reenviar email: {ex.Message}");
        }

        return NoContent();
    }

    // ── Aceitar convite (público, sem autenticação) ───────────────────────

    [HttpGet("aceitar/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> Aceitar(string token)
    {
        var p = await db.CompromissoParticipantes.FirstOrDefaultAsync(x => x.Token == token);

        if (p is null)
            return Content(HtmlResposta("Convite não encontrado", "Este link de confirmação não é válido ou já expirou.", "#D85A30"), "text/html; charset=utf-8");

        if (!p.Aceite)
        {
            p.Aceite = true;
            p.AceiteEm = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var compromisso = await db.Compromissos.FindAsync(p.CompromissoId);
        var titulo = compromisso?.Titulo ?? "compromisso";
        var quando = compromisso is not null
            ? compromisso.Inicio.ToString("dd/MM/yyyy 'às' HH:mm")
            : "";

        return Content(HtmlResposta(
            "Convite aceite!",
            $"Obrigado, <strong>{p.Nome}</strong>! A sua presença em <strong>{titulo}</strong>{(string.IsNullOrEmpty(quando) ? "" : $" ({quando})")} foi confirmada.",
            "#534AB7"), "text/html; charset=utf-8");
    }

    private static string HtmlResposta(string titulo, string mensagem, string cor) => $@"<!doctype html>
<html lang=""pt"">
<head>
  <meta charset=""utf-8""/>
  <meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
  <title>{titulo}</title>
  <style>
    body{{margin:0;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#F5F4F2;display:flex;align-items:center;justify-content:center;min-height:100vh}}
    .card{{background:#fff;border-radius:12px;padding:40px 32px;max-width:420px;width:90%;text-align:center;box-shadow:0 2px 12px rgba(0,0,0,.1)}}
    .icon{{font-size:48px;margin-bottom:16px}}
    h1{{color:{cor};font-size:1.5rem;margin:0 0 12px}}
    p{{color:#6B6A65;line-height:1.6;margin:0}}
    .footer{{margin-top:24px;color:#9E9D98;font-size:12px}}
  </style>
</head>
<body>
  <div class=""card"">
    <div class=""icon"">✓</div>
    <h1>{titulo}</h1>
    <p>{mensagem}</p>
    <div class=""footer"">Mapa Mensal — Gestão Pessoal</div>
  </div>
</body>
</html>";
}
