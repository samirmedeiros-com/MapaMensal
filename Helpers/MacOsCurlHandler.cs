using System.Diagnostics;
using System.Net;
using System.Text;

namespace MapaMensal.Helpers;

/// <summary>
/// macOS 16+ adds post-quantum MLKEM768 key shares in TLS that some servers reject.
/// curl uses LibreSSL which omits them, so on macOS we fall back to curl subprocess.
/// </summary>
public class MacOsCurlHandler(ILogger<MacOsCurlHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
            return await base.SendAsync(request, cancellationToken);

        var body = request.Content is not null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : "";

        var tmpFile = Path.GetTempFileName();
        try
        {
            // UTF8Encoding(false) = no BOM — Encoding.UTF8 emits BOM in .NET 10 WriteAllTextAsync
            await File.WriteAllTextAsync(tmpFile, body, new UTF8Encoding(false), cancellationToken);

            var psi = new ProcessStartInfo("curl")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false
            };

            // Use ArgumentList for safe argument passing (no shell escaping needed)
            psi.ArgumentList.Add("-s");
            psi.ArgumentList.Add("--http1.1");        // avoid HTTP/2 stream reset (CURLE_HTTP2)
            psi.ArgumentList.Add("-w");
            psi.ArgumentList.Add("\n%{http_code}");
            psi.ArgumentList.Add("-X");
            psi.ArgumentList.Add(request.Method.Method);

            foreach (var h in request.Headers)
                foreach (var v in h.Value)
                {
                    psi.ArgumentList.Add("-H");
                    psi.ArgumentList.Add($"{h.Key}: {v}");
                }

            if (request.Content is not null)
            {
                foreach (var h in request.Content.Headers)
                    foreach (var v in h.Value)
                    {
                        psi.ArgumentList.Add("-H");
                        psi.ArgumentList.Add($"{h.Key}: {v}");
                    }
                psi.ArgumentList.Add("--data-binary");
                psi.ArgumentList.Add($"@{tmpFile}");
            }

            psi.ArgumentList.Add(request.RequestUri!.ToString());

            using var proc = Process.Start(psi)!;
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            // The last line is the HTTP status code (from -w "\n%{http_code}")
            var lastNl = stdout.LastIndexOf('\n');
            string responseBody, statusStr;

            if (lastNl >= 0)
            {
                responseBody = stdout[..lastNl];
                statusStr    = stdout[(lastNl + 1)..].Trim();
            }
            else
            {
                responseBody = stdout;
                statusStr    = "200";
            }

            if (!int.TryParse(statusStr, out var statusCode) || statusCode == 0)
            {
                logger.LogWarning("curl exit={exit} status='{statusStr}' stderr={stderr} stdout={stdout}",
                    proc.ExitCode, statusStr, stderr, stdout);
                statusCode = 500;
            }
            else
            {
                logger.LogInformation("curl email → {status}: {body}", statusCode, responseBody);
            }

            return new HttpResponseMessage((HttpStatusCode)statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }
}
