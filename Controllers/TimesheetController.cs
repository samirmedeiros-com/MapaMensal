using Microsoft.AspNetCore.Authorization;
using MapaMensal.Data;
using MapaMensal.Models;
using MapaMensal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TimesheetController(AppDbContext db, ITocOnlineInvoiceService invoiceService) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] int year, [FromQuery] int month)
    {
        var approval = await db.TimesheetApprovals
            .FirstOrDefaultAsync(t => t.Year == year && t.Month == month);

        return Ok(new
        {
            IsApproved = approval?.IsApproved ?? false,
            ApprovedAt = approval?.ApprovedAt,
            ApprovedByUsername = approval?.ApprovedByUsername
        });
    }

    [HttpPost("aprovar")]
    public async Task<IActionResult> Aprovar([FromBody] TimesheetActionDto dto)
    {
        var approval = await db.TimesheetApprovals
            .FirstOrDefaultAsync(t => t.Year == dto.Year && t.Month == dto.Month);

        if (approval is null)
        {
            approval = new TimesheetApproval { Year = dto.Year, Month = dto.Month };
            db.TimesheetApprovals.Add(approval);
        }

        approval.IsApproved = true;
        approval.ApprovedAt = DateTime.UtcNow;
        approval.ApprovedByUsername = User.Identity?.Name;

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("cancelar-aprovacao")]
    public async Task<IActionResult> CancelarAprovacao([FromBody] TimesheetActionDto dto)
    {
        var approval = await db.TimesheetApprovals
            .FirstOrDefaultAsync(t => t.Year == dto.Year && t.Month == dto.Month);

        if (approval is not null)
        {
            approval.IsApproved = false;
            approval.ApprovedAt = null;
            approval.ApprovedByUsername = null;
            await db.SaveChangesAsync();
        }

        return Ok();
    }

    /// <summary>
    /// Para cada projeto devolve a fatura "atual": a ativa (não anulada) se existir,
    /// senão a anulada mais recente (para se poder mostrar o histórico/ícone de anulada).
    /// </summary>
    private async Task<TimesheetFatura?> GetFaturaAtualAsync(int projectId, int year, int month)
    {
        var todas = await db.TimesheetFaturas
            .Where(f => f.ProjectId == projectId && f.Year == year && f.Month == month)
            .ToListAsync();

        return todas
            .OrderBy(f => f.Estado == "Anulada" ? 1 : 0)
            .ThenByDescending(f => f.DataEmissao)
            .FirstOrDefault();
    }

    [HttpGet("faturas")]
    public async Task<IActionResult> GetFaturas([FromQuery] int year, [FromQuery] int month)
    {
        var todas = await db.TimesheetFaturas
            .Where(f => f.Year == year && f.Month == month)
            .ToListAsync();

        var atuais = todas
            .GroupBy(f => f.ProjectId)
            .Select(g =>
            {
                var atual = g
                    .OrderBy(f => f.Estado == "Anulada" ? 1 : 0)
                    .ThenByDescending(f => f.DataEmissao)
                    .First();
                return new
                {
                    atual.ProjectId,
                    atual.NumeroFatura,
                    atual.DataEmissao,
                    atual.Estado,
                    atual.DataRecebimento,
                    atual.Origem,
                    atual.AnuladaEm,
                    atual.JustificativaAnulacao,
                    TemPdf = atual.PdfBase64 != null,
                    TemAnuladas = g.Any(f => f.Estado == "Anulada")
                };
            });

        return Ok(atuais);
    }

    [HttpGet("faturas-anuladas/{projectId}/{year}/{month}")]
    public async Task<IActionResult> GetFaturasAnuladas(int projectId, int year, int month)
    {
        var anuladas = await db.TimesheetFaturas
            .Where(f => f.ProjectId == projectId && f.Year == year && f.Month == month && f.Estado == "Anulada")
            .OrderByDescending(f => f.AnuladaEm)
            .Select(f => new
            {
                f.Id,
                f.NumeroFatura,
                f.DataEmissao,
                f.AnuladaEm,
                f.JustificativaAnulacao,
                f.Origem,
                TemPdf = f.PdfBase64 != null
            })
            .ToListAsync();

        return Ok(anuladas);
    }

    [HttpGet("fatura-anulada/{faturaId}/pdf")]
    public async Task<IActionResult> GetFaturaAnuladaPdf(int faturaId)
    {
        var fatura = await db.TimesheetFaturas.FindAsync(faturaId);
        if (fatura?.PdfBase64 is null) return NotFound();

        var bytes = Convert.FromBase64String(fatura.PdfBase64);
        return File(bytes, "application/pdf", $"Fatura_{fatura.NumeroFatura}.pdf");
    }

    [HttpPost("emitir-fatura")]
    public async Task<IActionResult> EmitirFatura([FromBody] EmitirFaturaDto dto)
    {
        var resultado = await invoiceService.EmitirFaturaAsync(dto.ProjectId, dto.Year, dto.Month);
        if (!resultado.Sucesso) return UnprocessableEntity(resultado.Erro);

        return Ok(new
        {
            resultado.Fatura!.NumeroFatura,
            resultado.Fatura.DataEmissao,
            resultado.Fatura.Estado
        });
    }

    [HttpPost("emitir-fatura-offline")]
    public async Task<IActionResult> EmitirFaturaOffline([FromBody] EmitirFaturaOfflineDto dto)
    {
        var aprovado = await db.TimesheetApprovals
            .AnyAsync(a => a.Year == dto.Year && a.Month == dto.Month && a.IsApproved);
        if (!aprovado) return UnprocessableEntity("O TimeSheet tem de estar aprovado antes de emitir a fatura.");

        var existente = await db.TimesheetFaturas
            .AnyAsync(f => f.ProjectId == dto.ProjectId && f.Year == dto.Year && f.Month == dto.Month && f.Estado != "Anulada");
        if (existente) return UnprocessableEntity("Já existe uma fatura emitida para este projeto/mês.");

        if (string.IsNullOrWhiteSpace(dto.NumeroFatura) || string.IsNullOrWhiteSpace(dto.DataEmissao))
            return BadRequest("Número da fatura e data de emissão são obrigatórios.");

        var project = await db.Projects.FindAsync(dto.ProjectId);
        if (project is null) return NotFound("Projeto não encontrado.");

        var fatura = new TimesheetFatura
        {
            ProjectId = dto.ProjectId,
            Year = dto.Year,
            Month = dto.Month,
            NumeroFatura = dto.NumeroFatura.Trim(),
            DataEmissao = DateTime.Parse(dto.DataEmissao),
            PdfBase64 = dto.PdfBase64,
            Estado = "Emitida",
            Origem = "Offline"
        };
        db.TimesheetFaturas.Add(fatura);
        await FaturaFinanceiroHelper.CriarPrevisaoAsync(db, fatura, project);
        await db.SaveChangesAsync();

        return Ok(new { fatura.NumeroFatura, fatura.DataEmissao, fatura.Estado });
    }

    [HttpGet("fatura/{projectId}/{year}/{month}/pdf")]
    public async Task<IActionResult> GetFaturaPdf(int projectId, int year, int month)
    {
        var fatura = await GetFaturaAtualAsync(projectId, year, month);

        if (fatura?.PdfBase64 is null) return NotFound();

        var bytes = Convert.FromBase64String(fatura.PdfBase64);
        return File(bytes, "application/pdf", $"Fatura_{fatura.NumeroFatura}.pdf");
    }

    [HttpPost("anular-fatura")]
    public async Task<IActionResult> AnularFatura([FromBody] AnularFaturaDto dto)
    {
        var fatura = await GetFaturaAtualAsync(dto.ProjectId, dto.Year, dto.Month);
        if (fatura is null) return NotFound("Fatura não encontrada.");
        if (fatura.Estado != "Emitida")
            return UnprocessableEntity("Só é possível anular uma fatura ainda não recebida nem já anulada.");

        var (sucesso, erro) = await invoiceService.AnularFaturaAsync(fatura.Id, dto.Justificativa);
        if (!sucesso) return UnprocessableEntity(erro);

        return Ok();
    }

    [HttpPost("confirmar-recebimento")]
    public async Task<IActionResult> ConfirmarRecebimento([FromBody] EmitirFaturaDto dto)
    {
        var fatura = await db.TimesheetFaturas
            .Include(f => f.Project)
            .Where(f => f.ProjectId == dto.ProjectId && f.Year == dto.Year && f.Month == dto.Month && f.Estado != "Anulada")
            .OrderByDescending(f => f.DataEmissao)
            .FirstOrDefaultAsync();

        if (fatura is null) return NotFound("Fatura não encontrada.");
        if (fatura.Estado == "Recebida") return Ok();

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var lancamento = await db.ContasPessoais.FirstOrDefaultAsync(c => c.TimesheetFaturaId == fatura.Id);

        if (lancamento is null)
        {
            // Fatura emitida antes desta funcionalidade existir — cria o lançamento agora.
            var valorFatura = await FaturaFinanceiroHelper.CalcularValorFaturaAsync(db, dto.ProjectId, dto.Year, dto.Month);
            lancamento = new ContaPessoal
            {
                Tipo = "Entrada",
                Descricao = $"Fatura {fatura.NumeroFatura} — {fatura.Project.Name}",
                Categoria = "Faturação",
                DataVencimento = hoje,
                ValorPrevisto = valorFatura,
                MesReferencia = dto.Month,
                AnoReferencia = dto.Year,
                TimesheetFaturaId = fatura.Id
            };
            db.ContasPessoais.Add(lancamento);
        }

        lancamento.Pago = true;
        lancamento.DataPagamento = hoje;
        lancamento.ValorPago = lancamento.ValorPrevisto;

        fatura.Estado = "Recebida";
        fatura.DataRecebimento = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok();
    }
}

public record TimesheetActionDto(int Year, int Month);
public record EmitirFaturaDto(int ProjectId, int Year, int Month);
public record EmitirFaturaOfflineDto(int ProjectId, int Year, int Month, string NumeroFatura, string DataEmissao, string? PdfBase64);
public record AnularFaturaDto(int ProjectId, int Year, int Month, string Justificativa);
