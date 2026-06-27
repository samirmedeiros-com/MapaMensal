using MapaMensal.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Route("api/compromissos")]
public class ConviteController : ControllerBase
{
    private readonly AppDbContext _db;

    public ConviteController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("aceitar/{token}")]
    [AllowAnonymous]
    public async Task<ContentResult> Aceitar(string token)
    {
        var p = await _db.CompromissoParticipantes.FirstOrDefaultAsync(x => x.Token == token);

        if (p is null)
            return BuildPage("Convite não encontrado",
                "Este link de confirmação não é válido ou já expirou.", "#D85A30");

        if (!p.Aceite)
        {
            p.Aceite = true;
            p.AceiteEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        var compromisso = await _db.Compromissos.FindAsync(p.CompromissoId);
        var titulo = compromisso?.Titulo ?? "compromisso";
        var quando = compromisso is not null
            ? compromisso.Inicio.ToString("dd/MM/yyyy 'às' HH:mm")
            : "";
        var msg = "Obrigado, <strong>" + p.Nome + "</strong>! A sua presença em <strong>"
            + titulo + "</strong>"
            + (string.IsNullOrEmpty(quando) ? "" : " (" + quando + ")")
            + " foi confirmada.";

        return BuildPage("Convite aceite!", msg, "#534AB7");
    }

    private static ContentResult BuildPage(string titulo, string mensagem, string cor)
    {
        var html = "<!doctype html><html lang=\"pt\">"
            + "<head><meta charset=\"utf-8\"/>"
            + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/>"
            + "<title>" + titulo + "</title>"
            + "<style>body{margin:0;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;"
            + "background:#F5F4F2;display:flex;align-items:center;justify-content:center;min-height:100vh}"
            + ".card{background:#fff;border-radius:12px;padding:40px 32px;max-width:420px;width:90%;"
            + "text-align:center;box-shadow:0 2px 12px rgba(0,0,0,.1)}"
            + ".icon{font-size:48px;margin-bottom:16px}"
            + "h1{color:" + cor + ";font-size:1.5rem;margin:0 0 12px}"
            + "p{color:#6B6A65;line-height:1.6;margin:0}"
            + ".footer{margin-top:24px;color:#9E9D98;font-size:12px}</style>"
            + "</head><body><div class=\"card\">"
            + "<div class=\"icon\">&#10003;</div>"
            + "<h1>" + titulo + "</h1>"
            + "<p>" + mensagem + "</p>"
            + "<div class=\"footer\">Mapa Mensal &mdash; Gest&atilde;o Pessoal</div>"
            + "</div></body></html>";

        return new ContentResult
        {
            Content = html,
            ContentType = "text/html; charset=utf-8",
            StatusCode = 200
        };
    }
}
