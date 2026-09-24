using System.Text;
using System.Text.RegularExpressions;

namespace ZoomLog.Cliente;

/// Tira segredos do que vai ser guardado. Trabalha sobre texto e não sobre
/// JSON/XML interpretado: um corpo cortado a meio deixa de ser JSON válido,
/// e é exatamente esse que se guarda.
///
/// A regra é por nome de campo, igual em JSON, XML, formulário e cabeçalhos:
/// `"senha": "x"`, `<senha>x</senha>`, `senha="x"`, `senha=x&…`.
public sealed class Mascara
{
    public const string Oculto = "***";

    /// Os nomes da casa. Comparação sem maiúsculas e sem prefixo de namespace
    /// XML (`<ns2:senha>` também conta).
    public static readonly string[] DaCasa =
    [
        "senha", "password", "passwd", "pwd", "palavraPasse", "palavra", "novaSenha", "senhaAtual",
        "token", "accessToken", "access_token", "refreshToken", "refresh_token", "id_token", "idToken",
        "secret", "client_secret", "clientSecret", "segredo", "segredoWebhook",
        "apiKey", "api_key", "chaveApi", "chaveSecreta",
        "authorization", "cookie", "set-cookie", "proxy-authorization",
        "x-ponte", "x-integracao", "x-assinatura", "x-api-key", "x-zoomlog-chave", "x-chave",
        "cvv", "cvc", "numeroCartao", "card_number", "cardNumber",
        "privateKey", "chavePrivada", "pfx", "senhaCertificado", "certificadoSenha",
        "turnstile", "cf-turnstile-response",
    ];

    /// Qualquer nome que contenha um destes é segredo: `senhaConsultante` (o
    /// MNI dos tribunais), `clientSecret`, `novaPassword`. Nenhum destes
    /// aparece dentro do nome de um campo que não seja segredo.
    public static readonly string[] Contidos = ["senha", "password", "passwd", "secret", "segredo"];

    private readonly HashSet<string> nomes;
    private readonly Regex json;
    private readonly Regex xml;
    private readonly Regex atributo;
    private readonly Regex formulario;

    public Mascara(IEnumerable<string>? extra = null)
    {
        nomes = new HashSet<string>(DaCasa.Concat(extra ?? []), StringComparer.OrdinalIgnoreCase);
        var alternativa = string.Join("|", nomes.Select(Regex.Escape).OrderByDescending(n => n.Length))
            + "|" + string.Join("|", Contidos.Select(c => $@"[\w.-]*{c}[\w.-]*"));

        // "nome": "valor"  — também "nome":123 e "nome":null ficam tapados.
        json = new Regex(
            $@"(""(?:{alternativa})""\s*:\s*)(""(?:[^""\\]|\\.)*""?|[^,}}\]\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // <ns:nome atr="…">valor</ns:nome> — o valor até ao próximo «<».
        xml = new Regex(
            $@"(<(?:[\w.-]+:)?(?:{alternativa})(?:\s[^>]*)?>)([^<]*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // nome="valor" dentro de uma etiqueta.
        atributo = new Regex(
            $@"(\s(?:[\w.-]+:)?(?:{alternativa})\s*=\s*)(""[^""]*""|'[^']*')",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // nome=valor num formulário ou numa query string.
        formulario = new Regex(
            $@"((?:^|[?&;])(?:{alternativa})=)([^&;#\s]*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public bool Sensivel(string nome) =>
        nomes.Contains(nome) || Contidos.Any(c => nome.Contains(c, StringComparison.OrdinalIgnoreCase));

    public string? Texto(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;

        texto = json.Replace(texto, m => m.Groups[1].Value + $"\"{Oculto}\"");
        texto = xml.Replace(texto, m => m.Groups[1].Value + (m.Groups[2].Length == 0 ? "" : Oculto));
        texto = atributo.Replace(texto, m => m.Groups[1].Value + $"\"{Oculto}\"");
        texto = formulario.Replace(texto, m => m.Groups[1].Value + Oculto);
        return texto;
    }

    /// Um cabeçalho de autenticação diz *como* se autenticou — «Bearer», «Basic» —
    /// e isso ajuda a perceber um 401. O resto sai.
    public string Cabecalho(string nome, string valor)
    {
        if (!Sensivel(nome) && !PareceSegredo(nome)) return valor;

        var espaco = valor.IndexOf(' ');
        return espaco > 0 && espaco < 12 ? valor[..espaco] + " " + Oculto : Oculto;
    }

    public string Cabecalhos(IEnumerable<KeyValuePair<string, IEnumerable<string>>> cabecalhos)
    {
        var sb = new StringBuilder();
        foreach (var (nome, valores) in cabecalhos)
            foreach (var v in valores)
                sb.Append(nome).Append(": ").Append(Cabecalho(nome, v)).Append('\n');
        return sb.ToString();
    }

    /// Cabeçalhos com nomes inventados por cada integração — `X-Chave-Parceiro`,
    /// `X-Api-Token`. Melhor tapar um a mais do que guardar um a menos.
    private static bool PareceSegredo(string nome) =>
        nome.Contains("token", StringComparison.OrdinalIgnoreCase)
        || nome.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || nome.Contains("segredo", StringComparison.OrdinalIgnoreCase)
        || nome.Contains("senha", StringComparison.OrdinalIgnoreCase)
        || nome.Contains("password", StringComparison.OrdinalIgnoreCase)
        || nome.Contains("chave", StringComparison.OrdinalIgnoreCase)
        || nome.EndsWith("-key", StringComparison.OrdinalIgnoreCase)
        || nome.Contains("signature", StringComparison.OrdinalIgnoreCase)
        || nome.Contains("assinatura", StringComparison.OrdinalIgnoreCase);

    /// Corta e marca que cortou.
    public static string? Cortar(string? texto, int maximo, ref bool truncado)
    {
        if (texto is null || texto.Length <= maximo) return texto;
        truncado = true;
        return texto[..maximo];
    }

    /// Só se guarda como texto o que é texto. Um PDF ou uma imagem em base64
    /// dentro do log é espaço gasto e nada que alguém vá ler.
    public static bool ETexto(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return true;
        tipo = tipo.ToLowerInvariant();
        return tipo.StartsWith("text/")
            || tipo.Contains("json")
            || tipo.Contains("xml")
            || tipo.Contains("x-www-form-urlencoded")
            || tipo.Contains("javascript")
            || tipo.Contains("graphql");
    }
}
