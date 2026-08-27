using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace MapaMensal.Services;

/// Ponte para a API da Claude — usada para extrair dados de faturas/recibos
/// (PDF ou imagem) carregados pelo utilizador no Financeiro.
public class ClaudeService(IConfiguration config, ILogger<ClaudeService> logger)
{
    private readonly string _modelo = config["Claude:Model"] ?? "claude-haiku-4-5";
    private readonly AnthropicClient _cliente = new()
    {
        ApiKey = config["Claude:ApiKey"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
    };

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(config["Claude:ApiKey"])
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));

    private const string EsquemaExtracao = """
    {
      "type": "object",
      "properties": {
        "fornecedor": { "type": ["string", "null"], "description": "Nome da entidade/empresa que emitiu o documento" },
        "dataVencimento": { "type": ["string", "null"], "description": "Data de vencimento/pagamento no formato YYYY-MM-DD" },
        "valor": { "type": ["number", "null"], "description": "Valor total a pagar" },
        "entidade": { "type": ["string", "null"], "description": "Entidade do quadro de pagamento Multibanco (normalmente 5 dígitos) — NUNCA o Cód. Entidade / Código de Cliente / Nº de Cliente do fornecedor" },
        "referencia": { "type": ["string", "null"], "description": "Referência Multibanco (normalmente 9 dígitos)" },
        "moeda": { "type": ["string", "null"], "description": "Código ISO da moeda do valor (EUR, BRL, USD, etc.). Se o documento usar R$ é BRL; se usar € é EUR." }
      },
      "required": ["fornecedor", "dataVencimento", "valor", "entidade", "referencia", "moeda"],
      "additionalProperties": false
    }
    """;

    public async Task<FaturaExtraida> ExtrairFaturaAsync(string mimeType, string base64)
    {
        ContentBlockParam ficheiro = mimeType == "application/pdf"
            ? new DocumentBlockParam { Source = new Base64PdfSource { Data = base64 } }
            : new ImageBlockParam
            {
                Source = new Base64ImageSource
                {
                    Data = base64,
                    MediaType = MapearMediaType(mimeType),
                },
            };

        var esquema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(EsquemaExtracao)!;
        var resposta = await _cliente.Messages.Create(new MessageCreateParams
        {
            Model = _modelo,
            MaxTokens = 1024,
            System = "Extrais dados de faturas, recibos e avisos de pagamento portugueses. "
                   + "Devolves apenas os campos pedidos, em JSON. Se um campo não existir no "
                   + "documento, devolve null nesse campo. A data de vencimento vem sempre no "
                   + "formato YYYY-MM-DD. O valor é um número, sem símbolo de moeda.\n\n"
                   + "Atenção à Entidade: só interessa a Entidade do quadro de pagamento "
                   + "Multibanco/homebanking (o número, normalmente com 5 dígitos, que junto "
                   + "com a Referência serve para pagar o documento). NÃO confundir com \"Cód. "
                   + "Entidade\", \"Código de Cliente\", \"Nº de Cliente\" ou identificadores "
                   + "semelhantes que identificam o cliente/destinatário da fatura — esses NÃO "
                   + "são a Entidade de pagamento e devem ser ignorados.",
            OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = esquema } },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        ficheiro,
                        new TextBlockParam { Text = "Extrai o fornecedor, a data de vencimento, o valor, a moeda do valor e os dados do quadro de pagamento (Entidade e Referência Multibanco, não o código de cliente) deste documento." },
                    },
                },
            ],
        });

        var texto = string.Concat(resposta.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        try
        {
            return JsonSerializer.Deserialize<FaturaExtraida>(texto, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "A IA devolveu um JSON que não se consegue ler: {Texto}", texto);
            throw new InvalidOperationException("Não foi possível interpretar os dados do documento.");
        }
    }

    private static MediaType MapearMediaType(string mimeType) => mimeType switch
    {
        "image/png" => MediaType.ImagePng,
        "image/gif" => MediaType.ImageGif,
        "image/webp" => MediaType.ImageWebP,
        _ => MediaType.ImageJpeg,
    };
}

public record FaturaExtraida(string? Fornecedor, string? DataVencimento, decimal? Valor, string? Entidade, string? Referencia, string? Moeda);
