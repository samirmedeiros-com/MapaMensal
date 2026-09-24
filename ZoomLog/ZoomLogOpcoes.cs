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
    };

    // ---------------------------------------------------------------- pedidos

    /// Só se registam pedidos cujo caminho comece por um destes. O resto — os
    /// ficheiros da SPA — não é o que se procura num log.
    public List<string> CaminhosRegistados { get; set; } = ["/api"];
    public List<string> CaminhosIgnorados { get; set; } = ["/api/saude", "/api/health", "/healthz"];

    /// Corpo do pedido e da resposta de quem entra, para caminhos começados por
    /// um destes. Vazio por omissão: custa memória em cada pedido.
    public List<string> CorposEntrada { get; set; } = [];

    /// Guarda o corpo do pedido que entrou quando a resposta foi um erro (≥ 500).
    /// Barato: só se lê o que já está em memória e só quando falhou.
    public bool CorpoEntradaEmErro { get; set; } = true;

    // --------------------------------------------------------------- chamadas

    /// Anfitriões cujas chamadas levam sempre o corpo — `nfe.prefeitura.sp.gov.br`.
    /// Aceita `*.dominio` para os subdomínios.
    public List<string> CorposSaida { get; set; } = [];

    /// Guarda o corpo de uma chamada que falhou (exceção ou estado ≥ 400).
    public bool CorpoSaidaEmErro { get; set; } = true;

    /// Nunca se guarda o corpo destes, nem em erro. Os tribunais estão aqui
    /// por omissão: o MNI leva a senha dentro do XML e as respostas trazem
    /// processos, alguns em segredo de justiça. Para os ligar é preciso tirá-los
    /// desta lista *e* pô-los em `CorposSaida` — duas decisões, de propósito.
    public List<string> SemCorpo { get; set; } = ["*.jus.br"];

    /// Anfitriões que não se registam de todo (o próprio ZoomLog entra sempre).
    public List<string> SaidaIgnorada { get; set; } = [];

    // ------------------------------------------------------------- mascaramento

    /// Nomes de campos, cabeçalhos e parâmetros cujo valor sai como `***`.
    /// Acrescenta-se aos da casa (ver `Mascara`), não os substitui.
    public List<string> CamposSensiveis { get; set; } = [];

    // ------------------------------------------------------------------ limites

    /// Corte de cada corpo, em caracteres.
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
