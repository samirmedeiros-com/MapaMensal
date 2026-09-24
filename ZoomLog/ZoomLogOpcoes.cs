using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ZoomLog.Cliente;

/// Configuração do cliente, na secção `ZoomLog` do appsettings.
///
/// Sem `Endereco` ou sem `Chave` o cliente fica inerte: nada é recolhido e a
/// aplicação corre como corria. É o que deixa instalar isto em todo o lado e
/// ligar aplicação a aplicação.
public sealed class ZoomLogOpcoes
{
    public const string Seccao = "ZoomLog";

    /// Dentro do cluster: `http://zoomlog.default.svc.cluster.local`. No host
    /// (systemd): `http://10.96.200.10`. Só se recebe de dentro do servidor.
    public string Endereco { get; set; } = "";
    public string Chave { get; set; } = "";

    public string Ambiente { get; set; } = "prod";

    public bool Ativo => !string.IsNullOrWhiteSpace(Endereco) && !string.IsNullOrWhiteSpace(Chave);

    /// O que passa do `ILogger` para o ZoomLog. Os pedidos e as chamadas vão
    /// sempre — têm o seu próprio registo e não dependem disto.
    public LogLevel NivelMinimo { get; set; } = LogLevel.Information;

    /// As categorias da framework falam muito e dizem pouco ao nível Info.
    public Dictionary<string, LogLevel> NivelPorCategoria { get; set; } = new()
    {
        ["Microsoft"] = LogLevel.Warning,
        ["System"] = LogLevel.Warning,
        ["Microsoft.Hosting.Lifetime"] = LogLevel.Information,
        // Dois avisos em cada arranque («No XML encryptor configured», «Storing
        // keys in a directory…») sobre chaves que nenhuma aplicação da casa usa:
        // todas entram por JWT, sem cookies de sessão. Só um erro interessa.
        ["Microsoft.AspNetCore.DataProtection"] = LogLevel.Error,
    };

    // ---------------------------------------------------------------- pedidos

    /// Só se registam pedidos cujo caminho comece por um destes. O resto — os
    /// ficheiros da SPA — não é o que se procura num log.
    public List<string> CaminhosRegistados { get; set; } = ["/api"];
    public List<string> CaminhosIgnorados { get; set; } = ["/api/saude", "/api/health", "/healthz"];

    // --------------------------------------------------------------- chamadas

    // O JSON do pedido e o da resposta guardam-se sempre — em quem entra e em
    // cada chamada para fora. É obrigatório, por decisão do utilizador; o que
    // se escolhe aqui é só o que se tapa (`CamposSensiveis`) e o corte.

    /// Anfitriões que não se registam de todo (o próprio ZoomLog entra sempre).
    public List<string> SaidaIgnorada { get; set; } = [];

    // ------------------------------------------------------------- mascaramento

    /// Nomes de campos, cabeçalhos e parâmetros cujo valor sai como `***`.
    /// Acrescenta-se aos da casa (ver `Mascara`), não os substitui.
    public List<string> CamposSensiveis { get; set; } = [];

    // ------------------------------------------------------------------ limites

    /// Corte de cada corpo (o JSON do pedido ou da resposta), em caracteres.
    public int CorpoMaximo { get; set; } = 32 * 1024;

    /// Eventos à espera de envio. Cheia, perde-se o mais antigo e conta-se.
    public int Capacidade { get; set; } = 5000;
    public int Lote { get; set; } = 500;
    public int IntervaloMs { get; set; } = 2000;

    // ------------------------------------------------------------- identidade

    /// Quem está a usar a aplicação. Por omissão procura o email nos claims.
    public Func<HttpContext, string?>? UtilizadorDe { get; set; }

    /// De que cliente/escritório/empresa é o pedido. Por omissão procura
    /// claims com nomes habituais nas aplicações da casa.
    public Func<HttpContext, string?>? ClienteDe { get; set; }
}
