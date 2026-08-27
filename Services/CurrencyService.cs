using System.Text.Json;

namespace MapaMensal.Services;

/// <summary>Converte valores para EUR usando as taxas de câmbio do Banco Central Europeu (via frankfurter.app).</summary>
public class CurrencyService(IHttpClientFactory httpClientFactory, ILogger<CurrencyService> logger)
{
    public async Task<ConversaoMoeda> ConverterParaEurAsync(decimal valor, string moedaOrigem, CancellationToken ct = default)
    {
        if (moedaOrigem == "EUR") return new ConversaoMoeda(valor, valor, 1m, DateOnly.FromDateTime(DateTime.UtcNow));

        var client = httpClientFactory.CreateClient("frankfurter");
        var resposta = await client.GetAsync($"/latest?amount={valor.ToString(System.Globalization.CultureInfo.InvariantCulture)}&from={moedaOrigem}&to=EUR", ct);
        resposta.EnsureSuccessStatusCode();

        using var stream = await resposta.Content.ReadAsStreamAsync(ct);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var valorConvertido = doc.RootElement.GetProperty("rates").GetProperty("EUR").GetDecimal();
        var data = DateOnly.Parse(doc.RootElement.GetProperty("date").GetString()!);
        var taxa = valor != 0 ? valorConvertido / valor : 0;

        return new ConversaoMoeda(valor, valorConvertido, taxa, data);
    }

    public string GerarObservacao(ConversaoMoeda c, string moedaOrigem)
    {
        var simbolo = moedaOrigem == "BRL" ? "R$" : moedaOrigem;
        return $"Valor original: {simbolo} {c.ValorOriginal:N2} — convertido para € {c.ValorConvertido:N2} "
             + $"à cotação de {c.Taxa:N4} ({c.Data:dd/MM/yyyy}, BCE).";
    }
}

public record ConversaoMoeda(decimal ValorOriginal, decimal ValorConvertido, decimal Taxa, DateOnly Data);
