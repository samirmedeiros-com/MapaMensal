using System.Text;

namespace MapaMensal.Helpers;

public static class IcsHelper
{
    public static byte[] Gerar(string titulo, string? descricao, DateTime inicio, DateTime fim, string local, string uid)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//MapaMensal//Agenda//PT");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:REQUEST");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:{uid}@mapaemensal.pt");
        sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
        sb.AppendLine($"DTSTART:{inicio.ToUniversalTime():yyyyMMddTHHmmssZ}");
        sb.AppendLine($"DTEND:{fim.ToUniversalTime():yyyyMMddTHHmmssZ}");
        sb.AppendLine($"SUMMARY:{EscapeIcs(titulo)}");
        if (!string.IsNullOrEmpty(descricao))
            sb.AppendLine($"DESCRIPTION:{EscapeIcs(descricao)}");
        if (!string.IsNullOrEmpty(local))
            sb.AppendLine($"LOCATION:{EscapeIcs(local)}");
        sb.AppendLine("STATUS:CONFIRMED");
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");

        // RFC 5545 requires CRLF
        var ics = sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
        return Encoding.UTF8.GetBytes(ics);
    }

    private static string EscapeIcs(string s) =>
        s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");
}
