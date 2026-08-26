using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using MapaMensal.Data;
using MapaMensal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MapaMensal.Services;

public sealed record FaturaEmitidaResultado(bool Sucesso, string? Erro, TimesheetFatura? Fatura);

public interface ITocOnlineInvoiceService
{
    Task<FaturaEmitidaResultado> EmitirFaturaAsync(int projectId, int year, int month, CancellationToken ct = default);
    Task<(bool Sucesso, string? Erro)> AnularFaturaAsync(int faturaId, string justificativa, CancellationToken ct = default);
}

public class TocOnlineInvoiceService : ITocOnlineInvoiceService
{
    private readonly AppDbContext _db;
    private readonly TocOnlineOptions _opts;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ITocOnlineAuthService _auth;
    private readonly ILogger<TocOnlineInvoiceService> _logger;

    public TocOnlineInvoiceService(
        AppDbContext db, IOptions<TocOnlineOptions> opts, IHttpClientFactory httpFactory,
        ITocOnlineAuthService auth, ILogger<TocOnlineInvoiceService> logger)
    {
        _db = db;
        _opts = opts.Value;
        _httpFactory = httpFactory;
        _auth = auth;
        _logger = logger;
    }

    public async Task<FaturaEmitidaResultado> EmitirFaturaAsync(int projectId, int year, int month, CancellationToken ct)
    {
        var aprovado = await _db.TimesheetApprovals
            .AnyAsync(a => a.Year == year && a.Month == month && a.IsApproved, ct);
        if (!aprovado)
            return new(false, "O TimeSheet tem de estar aprovado antes de emitir a fatura.", null);

        var project = await _db.Projects.FindAsync([projectId], ct);
        if (project is null) return new(false, "Projeto não encontrado.", null);

        if (string.IsNullOrWhiteSpace(project.FaturacaoNomeFiscal) || string.IsNullOrWhiteSpace(project.FaturacaoNif))
            return new(false, "Preencha os dados de faturação do projeto (nome fiscal e NIF) em Configurações.", null);

        var existente = await _db.TimesheetFaturas
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Year == year && f.Month == month && f.Estado != "Anulada", ct);
        if (existente is not null)
            return new(false, "Já existe uma fatura emitida para este projeto/mês.", existente);

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var workedDays = await _db.WorkDays
            .Where(w => w.ProjectId == projectId && w.Date >= from && w.Date <= to && w.Mark > 0)
            .SumAsync(w => w.Mark, ct);

        if (workedDays <= 0)
            return new(false, "Não há dias trabalhados registados neste mês para este projeto.", null);

        var ivaRateStr = await _db.AppConfigs
            .Where(c => c.Key == "IvaRate")
            .Select(c => c.Value)
            .FirstOrDefaultAsync(ct) ?? "0.23";
        var ivaRate = decimal.Parse(ivaRateStr, CultureInfo.InvariantCulture);

        try
        {
            var token = await _auth.GetAccessTokenAsync();
            var client = _httpFactory.CreateClient("toconline");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = BuildDocumentPayload(project, workedDays, ivaRate, month, year);
            // TocOnline rejeita Content-Type com "charset" (415) — StringContent
            // acrescenta-o sempre, por isso o corpo tem de ir por ByteArrayContent
            // com o cabeçalho definido à mão (mesma solução usada no FleetDrive).
            var bytes = Encoding.UTF8.GetBytes(payload.ToJsonString());
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var postResp = await client.PostAsync($"{_opts.ApiUrl}/api/v1/commercial_sales_documents", content, ct);
            var postBody = await postResp.Content.ReadAsStringAsync(ct);
            if (!postResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("TocOnline respondeu {Status}: {Body}", postResp.StatusCode, postBody);
                return new(false, $"TocOnline retornou {(int)postResp.StatusCode}: {postBody}", null);
            }

            var postJson = JsonNode.Parse(postBody);
            var docId = postJson?["id"]?.ToString();
            var docNumber = postJson?["document_no"]?.GetValue<string>();
            if (string.IsNullOrEmpty(docId) || string.IsNullOrEmpty(docNumber))
                return new(false, $"TocOnline: resposta sem id/document_no: {postBody}", null);

            var pdfBase64 = await ObterPdfBase64Async(client, docId, ct);

            var fatura = new TimesheetFatura
            {
                ProjectId = projectId,
                Year = year,
                Month = month,
                NumeroFatura = docNumber,
                DataEmissao = DateTime.UtcNow,
                TocOnlineDocId = docId,
                PdfBase64 = pdfBase64,
                Estado = "Emitida",
                Origem = "Online"
            };
            _db.TimesheetFaturas.Add(fatura);
            await FaturaFinanceiroHelper.CriarPrevisaoAsync(_db, fatura, project, ct);
            await _db.SaveChangesAsync(ct);

            return new(true, null, fatura);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao emitir fatura TocOnline (projeto {ProjectId}, {Month}/{Year})", projectId, month, year);
            return new(false, ex.Message, null);
        }
    }

    public async Task<(bool Sucesso, string? Erro)> AnularFaturaAsync(int faturaId, string justificativa, CancellationToken ct)
    {
        var fatura = await _db.TimesheetFaturas.FindAsync([faturaId], ct);
        if (fatura is null) return (false, "Fatura não encontrada.");
        if (fatura.Estado == "Anulada") return (false, "Esta fatura já está anulada.");
        if (string.IsNullOrWhiteSpace(justificativa)) return (false, "É necessário indicar uma justificação.");

        if (fatura.Origem == "Online" && !string.IsNullOrEmpty(fatura.TocOnlineDocId))
        {
            try
            {
                var token = await _auth.GetAccessTokenAsync();
                var client = _httpFactory.CreateClient("toconline");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // TocOnline usa o inteiro 4 para "anulado" (não uma string "void") e exige
                // "voided_reason" — confirmado na documentação, depois de o pedido anterior
                // devolver 200 sem anular nada de facto (campo não reconhecido é ignorado).
                var body = new JsonObject { ["status"] = 4, ["voided_reason"] = justificativa };
                var bodyBytes = Encoding.UTF8.GetBytes(body.ToJsonString());
                var bodyContent = new ByteArrayContent(bodyBytes);
                bodyContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var req = new HttpRequestMessage(HttpMethod.Patch,
                    $"{_opts.ApiUrl}/api/v1/commercial_sales_documents/{fatura.TocOnlineDocId}")
                {
                    Content = bodyContent
                };
                var resp = await client.SendAsync(req, ct);
                var respBody = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("TocOnline recusou anulação {Status}: {Body}", resp.StatusCode, respBody);
                    return (false, $"TocOnline recusou a anulação ({(int)resp.StatusCode}): {respBody}");
                }

                // O TocOnline já aceitou (200) um pedido com um campo não reconhecido sem
                // anular nada — confirma-se sempre a seguir que o documento ficou mesmo anulado.
                var confirmResp = await client.GetAsync(
                    $"{_opts.ApiUrl}/api/v1/commercial_sales_documents/{fatura.TocOnlineDocId}", ct);
                var confirmBody = await confirmResp.Content.ReadAsStringAsync(ct);
                var statusAtual = JsonNode.Parse(confirmBody)?["status"]?.GetValue<int>();
                if (statusAtual != 4)
                {
                    _logger.LogWarning("TocOnline não anulou o documento {DocId}: status atual {Status}", fatura.TocOnlineDocId, statusAtual);
                    return (false, $"O TocOnline não confirmou a anulação (estado atual do documento: {statusAtual}). Verifique manualmente no TocOnline.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao anular fatura TocOnline {DocId}", fatura.TocOnlineDocId);
                return (false, ex.Message);
            }
        }

        fatura.Estado = "Anulada";
        fatura.AnuladaEm = DateTime.UtcNow;
        fatura.JustificativaAnulacao = justificativa;
        await FaturaFinanceiroHelper.RemoverPrevisaoAsync(_db, fatura.Id, ct);
        await _db.SaveChangesAsync(ct);

        return (true, null);
    }

    private static JsonObject BuildDocumentPayload(Project project, decimal workedDays, decimal ivaRate, int month, int year)
    {
        return new JsonObject
        {
            ["document_type"] = "FT",
            ["date"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ["customer_business_name"] = project.FaturacaoNomeFiscal,
            ["customer_tax_registration_number"] = project.FaturacaoNif,
            ["customer_address_detail"] = project.FaturacaoMorada ?? "",
            ["customer_postcode"] = project.FaturacaoCodigoPostal ?? "",
            ["customer_city"] = project.FaturacaoLocalidade ?? "",
            ["customer_country"] = string.IsNullOrWhiteSpace(project.FaturacaoPais) ? "PT" : project.FaturacaoPais,
            ["vat_included_prices"] = false,
            ["payment_mechanism"] = "TR", // Transferência bancária (o "TB" usado antes não é um código reconhecido pelo TocOnline)
            ["notes"] = $"TimeSheet {project.Name} — {month:D2}/{year}.",
            ["lines"] = new JsonArray
            {
                new JsonObject
                {
                    ["item_type"] = "Service",
                    ["description"] = $"CI - {month:D2}{year}",
                    ["quantity"] = workedDays,
                    ["unit_price"] = project.DailyRate,
                    ["tax_percentage"] = ivaRate * 100,
                    ["tax_country_region"] = "PT"
                }
            }
        };
    }

    private async Task<string?> ObterPdfBase64Async(HttpClient client, string docId, CancellationToken ct)
    {
        try
        {
            var pdfResp = await client.GetAsync(
                $"{_opts.ApiUrl}/api/url_for_print/{docId}?filter[type]=Document&filter[copies]=1", ct);
            if (!pdfResp.IsSuccessStatusCode) return null;

            var pdfBody = await pdfResp.Content.ReadAsStringAsync(ct);
            var pdfJson = JsonNode.Parse(pdfBody);
            var urlObj = pdfJson?["data"]?["attributes"]?["url"];
            var scheme = urlObj?["scheme"]?.GetValue<string>() ?? "https";
            var host = urlObj?["host"]?.GetValue<string>() ?? "";
            var path = urlObj?["path"]?.GetValue<string>() ?? "";
            if (string.IsNullOrEmpty(host)) return null;

            var pdfUrl = $"{scheme}://{host}{path}";
            var bytes = await client.GetByteArrayAsync(pdfUrl, ct);
            return Convert.ToBase64String(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao obter PDF da fatura TocOnline {DocId}", docId);
            return null;
        }
    }
}
