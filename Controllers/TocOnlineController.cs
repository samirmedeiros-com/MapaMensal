using System.Net.Http.Headers;
using MapaMensal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MapaMensal.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TocOnlineController(ITocOnlineAuthService auth, IHttpClientFactory httpFactory, IOptions<TocOnlineOptions> opts) : ControllerBase
{
    // Endpoint temporário de diagnóstico — remover depois de resolvido o problema da anulação.
    [HttpGet("debug-raw")]
    public async Task<IActionResult> DebugRaw([FromQuery] string path)
    {
        var token = await auth.GetAccessTokenAsync();
        var client = httpFactory.CreateClient("toconline");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.GetAsync($"{opts.Value.ApiUrl}{path}");
        var body = await resp.Content.ReadAsStringAsync();
        return Content(body, "application/json");
    }

    [HttpPost("debug-patch")]
    public async Task<IActionResult> DebugPatch([FromQuery] string path, [FromBody] System.Text.Json.Nodes.JsonObject body)
    {
        var token = await auth.GetAccessTokenAsync();
        var client = httpFactory.CreateClient("toconline");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var bytes = System.Text.Encoding.UTF8.GetBytes(body.ToJsonString());
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var req = new HttpRequestMessage(HttpMethod.Patch, $"{opts.Value.ApiUrl}{path}") { Content = content };
        var resp = await client.SendAsync(req);
        var respBody = await resp.Content.ReadAsStringAsync();
        return Ok(new { status = (int)resp.StatusCode, body = respBody });
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var configurado = await auth.IsConfiguredAsync();
        return Ok(new { isConfigured = configurado });
    }

    [HttpGet("auth-url")]
    public IActionResult GetAuthUrl() => Ok(new { url = auth.GetAuthUrl() });

    [HttpPost("exchange")]
    public async Task<IActionResult> ExchangeCode([FromBody] ExchangeCodeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Code)) return BadRequest("Código inválido.");

        var (sucesso, erro) = await auth.ExchangeCodeAsync(req.Code.Trim());
        return sucesso ? Ok() : BadRequest(erro);
    }
}

public record ExchangeCodeRequest(string Code);
