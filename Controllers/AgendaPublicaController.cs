using MapaMensal.Data;
using MapaMensal.Models;
using MapaMensal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Route("api/agenda-publica")]
public class AgendaPublicaController(AppDbContext db, IEmailService email) : ControllerBase
{
    public record SlotDto(DateTime Inicio, DateTime Fim);

    public record ReservaDto(
        string Nome, string Email,
        string? Telefone, string CodigoPais,
        DateTime Inicio, DateTime Fim);

    // GET /api/agenda-publica/status — verifica se agenda pública está activa
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var cfg = await db.AppConfigs
            .Where(c => c.Key == "agenda_publica_ativa" || c.Key == "agenda_publica_titulo")
            .ToListAsync();

        var ativa = cfg.FirstOrDefault(c => c.Key == "agenda_publica_ativa")?.Value == "true";
        var titulo = cfg.FirstOrDefault(c => c.Key == "agenda_publica_titulo")?.Value ?? "Agendar reunião";

        return Ok(new { ativa, titulo });
    }

    // GET /api/agenda-publica/slots?date=2026-06-27 — slots livres para um dia
    [HttpGet("slots")]
    public async Task<IActionResult> GetSlots([FromQuery] string date)
    {
        if (!DateOnly.TryParse(date, out var data))
            return BadRequest("Data inválida.");

        // Verifica se pública está activa
        var cfg = await db.AppConfigs.FirstOrDefaultAsync(c => c.Key == "agenda_publica_ativa");
        if (cfg?.Value != "true")
            return BadRequest("Agenda pública não está activa.");

        var diaSemana = (int)data.DayOfWeek;

        var horarios = await db.HorariosDisponiveis
            .Where(h => h.Ativo && h.DiaSemana == diaSemana)
            .ToListAsync();

        if (!horarios.Any())
            return Ok(Array.Empty<SlotDto>());

        // Duração global configurada (fallback: valor por horário)
        var duracaoCfg = await db.AppConfigs.FirstOrDefaultAsync(c => c.Key == "agenda_publica_duracao");
        int? duracaoGlobal = duracaoCfg != null && int.TryParse(duracaoCfg.Value, out var d) ? d : null;

        // Gera todos os slots possíveis
        var slots = new List<SlotDto>();
        foreach (var h in horarios)
        {
            var duracao = duracaoGlobal ?? h.DuracaoSlotMinutos;
            var cur = h.HoraInicio;
            while (cur.Add(TimeSpan.FromMinutes(duracao)) <= h.HoraFim)
            {
                var slotInicio = data.ToDateTime(TimeOnly.FromTimeSpan(cur));
                var slotFim = slotInicio.AddMinutes(duracao);
                slots.Add(new SlotDto(slotInicio, slotFim));
                cur = cur.Add(TimeSpan.FromMinutes(duracao));
            }
        }

        // Remove slots já ocupados por compromissos
        var iniciosDia = new DateTime(data.Year, data.Month, data.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var fimDia = iniciosDia.AddDays(1);

        var ocupados = await db.Compromissos
            .Where(c => c.Status != StatusCompromisso.Cancelado
                     && c.Inicio >= iniciosDia && c.Inicio < fimDia)
            .Select(c => new { c.Inicio, c.Fim })
            .ToListAsync();

        var livres = slots.Where(s =>
            !ocupados.Any(o => s.Inicio < o.Fim && s.Fim > o.Inicio)
            && s.Inicio > DateTime.UtcNow.AddHours(1) // mínimo 1h de antecedência
        ).ToList();

        return Ok(livres);
    }

    // POST /api/agenda-publica/reservar — efetua uma marcação pública
    [HttpPost("reservar")]
    public async Task<IActionResult> Reservar([FromBody] ReservaDto dto)
    {
        var cfg = await db.AppConfigs
            .Where(c => c.Key == "agenda_publica_ativa" || c.Key == "agenda_publica_titulo")
            .ToListAsync();

        if (cfg.FirstOrDefault(c => c.Key == "agenda_publica_ativa")?.Value != "true")
            return BadRequest("Agenda pública não está activa.");

        var titulo = cfg.FirstOrDefault(c => c.Key == "agenda_publica_titulo")?.Value ?? "Reunião";

        // Verifica se o slot ainda está livre
        var ocupado = await db.Compromissos.AnyAsync(c =>
            c.Status != StatusCompromisso.Cancelado
            && c.Inicio < dto.Fim && c.Fim > dto.Inicio);

        if (ocupado)
            return Conflict("Este horário já não está disponível.");

        var compromisso = new Compromisso
        {
            Titulo = $"{titulo} — {dto.Nome}",
            Descricao = $"Marcação pública de {dto.Nome} ({dto.Email})",
            Inicio = dto.Inicio,
            Fim = dto.Fim,
            Tipo = TipoCompromisso.Publico,
            Status = StatusCompromisso.Agendado,
            NotificarParticipantes = true,
            CriadoEm = DateTime.UtcNow,
            Participantes = new List<CompromissoParticipante>
            {
                new()
                {
                    Nome = dto.Nome,
                    Email = dto.Email,
                    Telefone = dto.Telefone,
                    CodigoPais = dto.CodigoPais,
                    Notificar = true
                }
            }
        };

        db.Compromissos.Add(compromisso);
        await db.SaveChangesAsync();

        // Envia confirmação ao participante
        try
        {
            await email.SendConfirmacaoPublicaAsync(
                dto.Email, dto.Nome, titulo,
                dto.Inicio, dto.Fim, "");
        }
        catch { /* não falha a reserva por erro de email */ }

        return Ok(new { compromisso.Id, message = "Marcação efectuada com sucesso!" });
    }

    // GET /api/agenda-publica/config — configuração pública da agenda
    [HttpGet("config")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> GetConfig()
    {
        var cfgs = await db.AppConfigs
            .Where(c => c.Key.StartsWith("agenda_publica_"))
            .ToListAsync();

        return Ok(cfgs.ToDictionary(c => c.Key, c => c.Value));
    }

    // PUT /api/agenda-publica/config — guardar configuração
    [HttpPut("config")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> SaveConfig([FromBody] Dictionary<string, string> valores)
    {
        foreach (var kv in valores)
        {
            if (!kv.Key.StartsWith("agenda_publica_")) continue;
            var cfg = await db.AppConfigs.FirstOrDefaultAsync(c => c.Key == kv.Key);
            if (cfg is null)
                db.AppConfigs.Add(new AppConfig { Key = kv.Key, Value = kv.Value });
            else
                cfg.Value = kv.Value;
        }
        await db.SaveChangesAsync();
        return NoContent();
    }
}
