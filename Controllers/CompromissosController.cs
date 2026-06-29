using MapaMensal.Data;
using MapaMensal.Models;
using MapaMensal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    public record RecorrenciaDto(
        string Frequencia,       // "diaria" | "semanal" | "mensal" | "anual"
        int Intervalo,           // every N units
        List<int>? DiasSemana,   // 1=Seg..7=Dom  (only for semanal)
        string Fim,              // "nunca" | "data" | "ocorrencias"
        string? FimData,         // ISO date  (when Fim=="data")
        int? FimOcorrencias);    // count     (when Fim=="ocorrencias")

    public record CompromissoCreateDto(
        string Titulo, string? Descricao,
        DateTime Inicio, DateTime Fim,
        int? ProjectId, int? ContaPessoalId,
        string Local, bool Online, string? LinkOnline,
        TipoCompromisso Tipo,
        bool NotificarParticipantes,
        List<ParticipanteDto> Participantes,
        RecorrenciaDto? Recorrencia = null,
        string? Cor = null,
        int? CategoriaId = null);

    public record CompromissoUpdateStatusDto(StatusCompromisso Status);

    // ── Recorrência: geração de ocorrências ───────────────────────────────

    private static IEnumerable<(DateTime Inicio, DateTime Fim)> GerarOcorrencias(
        DateTime iniBase, DateTime fimBase, RecorrenciaDto rec)
    {
        var duracao = fimBase - iniBase;
        var limite = rec.Fim == "data" && !string.IsNullOrEmpty(rec.FimData)
            ? DateTime.Parse(rec.FimData).Date.AddDays(1)
            : iniBase.AddYears(2);
        var maxCount = rec.Fim == "ocorrencias" ? Math.Min(rec.FimOcorrencias ?? 10, 365) : 365;

        int count = 0;

        if (rec.Frequencia == "semanal" && rec.DiasSemana?.Count > 0)
        {
            var dias = rec.DiasSemana.Distinct().OrderBy(d => d).ToList();
            // Semana de segunda (dia 1) que contém iniBase
            var dow = (int)iniBase.DayOfWeek;
            var semana = iniBase.Date.AddDays(-((dow + 6) % 7));

            while (semana < limite && count < maxCount)
            {
                foreach (var d in dias)
                {
                    var data = semana.AddDays(d - 1);  // d=1→Seg, d=7→Dom
                    if (data >= iniBase.Date && data < limite && count < maxCount)
                    {
                        var ini = data + iniBase.TimeOfDay;
                        yield return (ini, ini + duracao);
                        count++;
                    }
                }
                semana = semana.AddDays(7 * rec.Intervalo);
            }
        }
        else
        {
            var atual = iniBase;
            while (atual < limite && count < maxCount)
            {
                yield return (atual, atual + duracao);
                count++;
                atual = rec.Frequencia switch
                {
                    "diaria"  => atual.AddDays(rec.Intervalo),
                    "semanal" => atual.AddDays(7 * rec.Intervalo),
                    "mensal"  => atual.AddMonths(rec.Intervalo),
                    "anual"   => atual.AddYears(rec.Intervalo),
                    _         => limite  // stop
                };
            }
        }
    }

    private CompromissoParticipante MapParticipante(ParticipanteDto p, int compromissoId = 0) =>
        new()
        {
            CompromissoId = compromissoId,
            Nome = p.Nome,
            Email = p.Email,
            Telefone = p.Telefone,
            CodigoPais = p.CodigoPais,
            Notificar = p.Notificar,
            Token = Guid.NewGuid().ToString("N")
        };

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

    // ── CRUD ──────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? ano, [FromQuery] int? mes)
    {
        var q = _db.Compromissos
            .Include(c => c.Participantes)
            .Include(c => c.Project)
            .Include(c => c.Categoria)
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
            .Include(c => c.Categoria)
            .FirstOrDefaultAsync(c => c.Id == id);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CompromissoCreateDto dto)
    {
        if (dto.Recorrencia != null)
        {
            var recId = Guid.NewGuid().ToString("N");
            var ocorrencias = GerarOcorrencias(dto.Inicio, dto.Fim, dto.Recorrencia).ToList();

            Compromisso? primeiro = null;
            foreach (var (ini, fim) in ocorrencias)
            {
                var c = new Compromisso
                {
                    Titulo = dto.Titulo,
                    Descricao = dto.Descricao,
                    Inicio = ini,
                    Fim = fim,
                    ProjectId = dto.ProjectId,
                    ContaPessoalId = dto.ContaPessoalId,
                    Local = dto.Local,
                    Online = dto.Online,
                    LinkOnline = dto.LinkOnline,
                    Tipo = dto.Tipo,
                    Status = StatusCompromisso.Agendado,
                    NotificarParticipantes = dto.NotificarParticipantes,
                    CriadoEm = DateTime.UtcNow,
                    RecorrenciaId = recId,
                    Cor = dto.Cor,
                    CategoriaId = dto.CategoriaId,
                    Participantes = dto.Participantes.Select(p => MapParticipante(p)).ToList()
                };
                _db.Compromissos.Add(c);
                primeiro ??= c;
            }

            await _db.SaveChangesAsync();

            // Notificar apenas na primeira ocorrência
            if (dto.NotificarParticipantes && primeiro != null)
            {
                try
                {
                    await Task.WhenAll(primeiro.Participantes
                        .Where(p => p.Notificar && !string.IsNullOrEmpty(p.Email))
                        .Select(p => _email.SendConviteCompromissoAsync(
                            p.Email, p.Nome, primeiro.Titulo,
                            primeiro.Inicio, primeiro.Fim,
                            primeiro.Local, primeiro.Descricao,
                            _baseUrl + "/api/compromissos/aceitar/" + p.Token)));
                }
                catch { /* não falha a criação */ }
            }

            return CreatedAtAction(nameof(GetById), new { id = primeiro!.Id }, primeiro);
        }

        // Sem recorrência — fluxo original
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
            Status = StatusCompromisso.Agendado,
            NotificarParticipantes = dto.NotificarParticipantes,
            CriadoEm = DateTime.UtcNow,
            Cor = dto.Cor,
            CategoriaId = dto.CategoriaId,
            Participantes = dto.Participantes.Select(p => MapParticipante(p)).ToList()
        };

        _db.Compromissos.Add(compromisso);
        await _db.SaveChangesAsync();

        if (dto.NotificarParticipantes)
        {
            try
            {
                await Task.WhenAll(compromisso.Participantes
                    .Where(p => p.Notificar && !string.IsNullOrEmpty(p.Email))
                    .Select(p => _email.SendConviteCompromissoAsync(
                        p.Email, p.Nome, compromisso.Titulo,
                        compromisso.Inicio, compromisso.Fim,
                        compromisso.Local, compromisso.Descricao,
                        _baseUrl + "/api/compromissos/aceitar/" + p.Token)));
            }
            catch { }
        }

        return CreatedAtAction(nameof(GetById), new { id = compromisso.Id }, compromisso);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id, [FromBody] CompromissoCreateDto dto,
        [FromQuery] string escopo = "este")
    {
        var compromisso = await _db.Compromissos
            .Include(c => c.Participantes)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (compromisso is null) return NotFound();

        if (escopo == "todos" && compromisso.RecorrenciaId != null)
        {
            var serie = await _db.Compromissos
                .Include(c => c.Participantes)
                .Where(c => c.RecorrenciaId == compromisso.RecorrenciaId)
                .ToListAsync();

            foreach (var c in serie)
            {
                var antigos = c.Participantes.ToDictionary(p => p.Email, p => (p.Token, p.Aceite, p.AceiteEm));

                c.Titulo = dto.Titulo;
                c.Descricao = dto.Descricao;
                c.Local = dto.Local;
                c.Online = dto.Online;
                c.LinkOnline = dto.LinkOnline;
                c.Tipo = dto.Tipo;
                c.ProjectId = dto.ProjectId;
                c.ContaPessoalId = dto.ContaPessoalId;
                c.NotificarParticipantes = dto.NotificarParticipantes;
                c.Cor = dto.Cor;
                c.CategoriaId = dto.CategoriaId;
                // Hora do dia actualizada, data mantém-se
                var offset = dto.Fim - dto.Inicio;
                c.Inicio = c.Inicio.Date + dto.Inicio.TimeOfDay;
                c.Fim = c.Inicio + offset;

                _db.CompromissoParticipantes.RemoveRange(c.Participantes.ToList());
                c.Participantes = dto.Participantes.Select(p =>
                {
                    var ant = antigos.GetValueOrDefault(p.Email);
                    return new CompromissoParticipante
                    {
                        CompromissoId = c.Id,
                        Nome = p.Nome, Email = p.Email, Telefone = p.Telefone,
                        CodigoPais = p.CodigoPais, Notificar = p.Notificar,
                        Token = ant.Token ?? Guid.NewGuid().ToString("N"),
                        Aceite = ant.Aceite, AceiteEm = ant.AceiteEm
                    };
                }).ToList();
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Escopo "este": desliga da série e actualiza só este
        var dadosAntigos = compromisso.Participantes.ToDictionary(p => p.Email, p => (p.Token, p.Aceite, p.AceiteEm));

        compromisso.RecorrenciaId = null;  // desliga da série
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
        compromisso.Cor = dto.Cor;
        compromisso.CategoriaId = dto.CategoriaId;

        _db.CompromissoParticipantes.RemoveRange(compromisso.Participantes.ToList());
        compromisso.Participantes = dto.Participantes.Select(p =>
        {
            var ant = dadosAntigos.GetValueOrDefault(p.Email);
            return new CompromissoParticipante
            {
                CompromissoId = id,
                Nome = p.Nome, Email = p.Email, Telefone = p.Telefone,
                CodigoPais = p.CodigoPais, Notificar = p.Notificar,
                Token = ant.Token ?? Guid.NewGuid().ToString("N"),
                Aceite = ant.Aceite, AceiteEm = ant.AceiteEm
            };
        }).ToList();

        await _db.SaveChangesAsync();

        if (dto.NotificarParticipantes)
        {
            try
            {
                await Task.WhenAll(compromisso.Participantes
                    .Where(p => p.Notificar && !string.IsNullOrEmpty(p.Email))
                    .Select(p => _email.SendConviteAlteradoAsync(
                        p.Email, p.Nome, compromisso.Titulo,
                        compromisso.Inicio, compromisso.Fim,
                        compromisso.Local, compromisso.Descricao,
                        _baseUrl + "/api/compromissos/aceitar/" + p.Token)));
            }
            catch { }
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
    public async Task<IActionResult> Delete(int id, [FromQuery] string escopo = "este")
    {
        var c = await _db.Compromissos.FindAsync(id);
        if (c is null) return NotFound();

        if (escopo == "todos" && c.RecorrenciaId != null)
        {
            var serie = await _db.Compromissos
                .Where(c2 => c2.RecorrenciaId == c.RecorrenciaId)
                .ToListAsync();
            _db.Compromissos.RemoveRange(serie);
        }
        else
        {
            _db.Compromissos.Remove(c);
        }

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
            await Task.WhenAll(compromisso.Participantes
                .Where(p => p.Notificar && !string.IsNullOrEmpty(p.Email))
                .Select(p => _email.SendConviteCompromissoAsync(
                    p.Email, p.Nome, compromisso.Titulo,
                    compromisso.Inicio, compromisso.Fim,
                    compromisso.Local, compromisso.Descricao,
                    _baseUrl + "/api/compromissos/aceitar/" + p.Token)));
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Erro ao reenviar email: " + ex.Message);
        }

        return NoContent();
    }
}
