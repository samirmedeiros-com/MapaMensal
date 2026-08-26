namespace MapaMensal.Models;

/// <summary>Linha única (Id=1) com os tokens OAuth do TocOnline.</summary>
public class TocOnlineToken
{
    public int Id { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessExpiry { get; set; }
}
