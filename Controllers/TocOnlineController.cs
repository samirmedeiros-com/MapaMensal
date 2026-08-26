using MapaMensal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MapaMensal.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TocOnlineController(ITocOnlineAuthService auth) : ControllerBase
{
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
