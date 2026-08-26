using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using MapaMensal.Data;
using MapaMensal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MapaMensal.Services;

public interface ITocOnlineAuthService
{
    Task<bool> IsConfiguredAsync();
    Task<string> GetAccessTokenAsync();
    Task<(bool Sucesso, string? Erro)> ExchangeCodeAsync(string code);
    string GetAuthUrl();
}

public class TocOnlineAuthService : ITocOnlineAuthService
{
    private readonly AppDbContext _db;
    private readonly TocOnlineOptions _opts;
    private readonly IHttpClientFactory _httpFactory;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public TocOnlineAuthService(AppDbContext db, IOptions<TocOnlineOptions> opts, IHttpClientFactory httpFactory)
    {
        _db = db;
        _opts = opts.Value;
        _httpFactory = httpFactory;
    }

    public string GetAuthUrl()
    {
        return $"{_opts.OAuthUrl}/auth" +
               $"?client_id={Uri.EscapeDataString(_opts.ClientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(_opts.RedirectUri)}" +
               $"&response_type=code" +
               $"&scope=commercial";
    }

    public async Task<bool> IsConfiguredAsync()
    {
        var token = await _db.TocOnlineTokens.FirstOrDefaultAsync(t => t.Id == 1);
        return token is not null && !string.IsNullOrEmpty(token.RefreshToken);
    }

    public async Task<string> GetAccessTokenAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var token = await _db.TocOnlineTokens.FirstOrDefaultAsync(t => t.Id == 1);
            if (token is null)
                throw new InvalidOperationException("TocOnline: ainda não foi autorizado o acesso.");

            if (DateTime.UtcNow < token.AccessExpiry.AddSeconds(-60))
                return token.AccessToken;

            var refreshed = await RefreshAsync(token);
            if (!refreshed)
                throw new InvalidOperationException("TocOnline: não foi possível renovar o acesso, é necessário autorizar novamente.");

            return token.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<(bool Sucesso, string? Erro)> ExchangeCodeAsync(string code)
    {
        try
        {
            var client = _httpFactory.CreateClient("toconline-auth");
            var basicCreds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.ClientSecret}"));
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_opts.OAuthUrl}/token");
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicCreds);
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _opts.RedirectUri,
                ["scope"] = "commercial"
            });

            var resp = await client.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return (false, $"TocOnline retornou {(int)resp.StatusCode}: {body}");

            await SalvarTokensAsync(body);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<bool> RefreshAsync(TocOnlineToken token)
    {
        try
        {
            var client = _httpFactory.CreateClient("toconline-auth");
            var basicCreds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.ClientSecret}"));
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_opts.OAuthUrl}/token");
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicCreds);
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = token.RefreshToken,
                ["scope"] = "commercial"
            });

            var resp = await client.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return false;

            var body = await resp.Content.ReadAsStringAsync();
            await SalvarTokensAsync(body);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task SalvarTokensAsync(string tokenJson)
    {
        var json = JsonNode.Parse(tokenJson);
        var accessToken = json?["access_token"]?.GetValue<string>()
            ?? throw new InvalidOperationException("TocOnline: access_token ausente na resposta.");
        var newRefresh = json?["refresh_token"]?.GetValue<string>();
        var expiresIn = json?["expires_in"]?.GetValue<int>() ?? 14400;

        var token = await _db.TocOnlineTokens.FirstOrDefaultAsync(t => t.Id == 1);
        if (token is null)
        {
            token = new TocOnlineToken { Id = 1 };
            _db.TocOnlineTokens.Add(token);
        }

        token.AccessToken = accessToken;
        if (!string.IsNullOrEmpty(newRefresh))
            token.RefreshToken = newRefresh;
        token.AccessExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

        await _db.SaveChangesAsync();
    }
}
