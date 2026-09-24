using System.Net;
using Microsoft.AspNetCore.Http;

namespace ZoomLog.Cliente;

/// O endereço de quem fez o pedido, visto de dentro de um pod.
///
/// O caminho até cá: HAProxy (modo TCP, com PROXY protocol) → ingress-nginx →
/// pod. O ingress lê o IP verdadeiro do PROXY protocol e põe-no em
/// `X-Real-IP`; esse cabeçalho é escrito por ele e não vem do browser.
///
/// O `X-Forwarded-For` não serve para isto: com `compute-full-forwarded-for`
/// o ingress *acrescenta* ao que o cliente mandou, e qualquer pessoa escreve
/// o que quiser no início dele. Só a última entrada é do ingress.
///
/// Os domínios que passam pela Cloudflare chegam com o IP da Cloudflare. Aí,
/// e só aí, vale o `CF-Connecting-IP` — confiar nele vindo de outro sítio
/// seria deixar o cliente escolher o seu IP.
public static class IpCliente
{
    /// https://www.cloudflare.com/ips-v4 e /ips-v6. Mudam raramente; quando
    /// mudarem, o pior que acontece é aparecer o IP da Cloudflare em vez do
    /// do cliente — nunca um IP inventado.
    private static readonly IPNetwork[] Cloudflare =
    [
        .. new[]
        {
            "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22",
            "141.101.64.0/18", "108.162.192.0/18", "190.93.240.0/20", "188.114.96.0/20",
            "197.234.240.0/22", "198.41.128.0/17", "162.158.0.0/15", "104.16.0.0/13",
            "104.24.0.0/14", "172.64.0.0/13", "131.0.72.0/22",
            "2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32",
            "2405:8100::/32", "2a06:98c0::/29", "2c0f:f248::/32",
        }.Select(IPNetwork.Parse),
    ];

    public static string? De(HttpContext ctx) => De(
        ctx.Request.Headers["X-Real-IP"].ToString(),
        ctx.Request.Headers["X-Forwarded-For"].ToString(),
        ctx.Request.Headers["CF-Connecting-IP"].ToString(),
        ctx.Connection.RemoteIpAddress);

    /// A decisão, separada do `HttpContext` para se pôr à prova sem servidor.
    public static string? De(string? realIp, string? forwardedFor, string? cfConnectingIp, IPAddress? ligacao)
    {
        var ip = Ler(realIp);

        if (ip is null && !string.IsNullOrWhiteSpace(forwardedFor))
            ip = Ler(forwardedFor.Split(',')[^1]);

        ip ??= ligacao;
        if (ip is null) return null;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

        if (DaCloudflare(ip) && Ler(cfConnectingIp) is { } real)
            return (real.IsIPv4MappedToIPv6 ? real.MapToIPv4() : real).ToString();

        return ip.ToString();
    }

    public static bool DaCloudflare(IPAddress ip) => Cloudflare.Any(r => r.Contains(ip));

    private static IPAddress? Ler(string? texto) =>
        IPAddress.TryParse((texto ?? "").Trim(), out var ip) ? ip : null;
}
