namespace MapaMensal.Services;

public class TocOnlineOptions
{
    public const string Section = "TocOnline";

    public string OAuthUrl { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "https://oauth.pstmn.io/v1/callback";
}
