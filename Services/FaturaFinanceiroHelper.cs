using MapaMensal.Data;
using MapaMensal.Models;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Services;

/// <summary>Liga a emissão/anulação/recebimento de faturas do TimeSheet aos lançamentos automáticos no Financeiro.</summary>
public static class FaturaFinanceiroHelper
{
    public static async Task<decimal> CalcularValorFaturaAsync(AppDbContext db, int projectId, int year, int month, CancellationToken ct = default)
    {
        var project = await db.Projects.FindAsync([projectId], ct);
        if (project is null) return 0;

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var workedDays = await db.WorkDays
            .Where(w => w.ProjectId == projectId && w.Date >= from && w.Date <= to && w.Mark > 0)
            .SumAsync(w => w.Mark, ct);

        var ivaRate = await ObterIvaRateAsync(db, ct);
        return workedDays * project.DailyRate * (1 + ivaRate);
    }

    public static async Task<decimal> ObterIvaRateAsync(AppDbContext db, CancellationToken ct = default)
    {
        var ivaRateStr = await db.AppConfigs
            .Where(c => c.Key == "IvaRate")
            .Select(c => c.Value)
            .FirstOrDefaultAsync(ct) ?? "0.23";
        return decimal.Parse(ivaRateStr, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static async Task<(string? Iban, string? Bic, string? Titular)> ObterDadosBancariosAsync(AppDbContext db, CancellationToken ct = default)
    {
        var configs = await db.AppConfigs
            .Where(c => c.Key == "Banco:Iban" || c.Key == "Banco:Bic" || c.Key == "Banco:Titular")
            .ToDictionaryAsync(c => c.Key, c => c.Value, ct);
        return (
            configs.GetValueOrDefault("Banco:Iban"),
            configs.GetValueOrDefault("Banco:Bic"),
            configs.GetValueOrDefault("Banco:Titular")
        );
    }

    /// <summary>
    /// Calcula a data de vencimento a partir do dia do mês configurado no projeto: se esse dia ainda
    /// não tiver passado no mês da emissão, fica nesse mês; senão passa para o mês seguinte — nunca
    /// antes (nem no mesmo dia, sempre posterior) da data de emissão.
    /// </summary>
    public static DateOnly CalcularDataVencimento(DateOnly dataEmissao, int diaVencimento)
    {
        DateOnly ParaDiaValido(int ano, int mes, int dia)
        {
            var diasNoMes = DateTime.DaysInMonth(ano, mes);
            return new DateOnly(ano, mes, Math.Min(dia, diasNoMes));
        }

        var candidato = ParaDiaValido(dataEmissao.Year, dataEmissao.Month, diaVencimento);
        if (candidato > dataEmissao) return candidato;

        var proximoMes = dataEmissao.AddMonths(1);
        return ParaDiaValido(proximoMes.Year, proximoMes.Month, diaVencimento);
    }

    /// <summary>Cria em Contas Pessoais a previsão de entrada correspondente a uma fatura recém-emitida.</summary>
    public static async Task CriarPrevisaoAsync(AppDbContext db, TimesheetFatura fatura, Project project, CancellationToken ct = default)
    {
        var valorFatura = await CalcularValorFaturaAsync(db, fatura.ProjectId, fatura.Year, fatura.Month, ct);
        var dataVencimento = CalcularDataVencimento(DateOnly.FromDateTime(fatura.DataEmissao), project.DiaVencimento);

        db.ContasPessoais.Add(new ContaPessoal
        {
            Tipo = "Entrada",
            Descricao = $"Fatura {fatura.NumeroFatura} — {project.Name}",
            Categoria = "Faturação",
            DataVencimento = dataVencimento,
            ValorPrevisto = valorFatura,
            Pago = false,
            MesReferencia = fatura.Month,
            AnoReferencia = fatura.Year,
            TimesheetFaturaId = fatura.Id
        });
    }

    /// <summary>Remove a previsão associada quando a fatura é anulada (nunca esteve paga, pois só se anula antes do recebimento).</summary>
    public static async Task RemoverPrevisaoAsync(AppDbContext db, int faturaId, CancellationToken ct = default)
    {
        var lancamento = await db.ContasPessoais
            .FirstOrDefaultAsync(c => c.TimesheetFaturaId == faturaId && !c.Pago, ct);
        if (lancamento is not null)
            db.ContasPessoais.Remove(lancamento);
    }
}
