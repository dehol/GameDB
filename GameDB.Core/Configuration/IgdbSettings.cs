namespace GameDB.Core.Configuration;

public class IgdbSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = "https://id.twitch.tv/oauth2/token";
    public string ApiBaseUrl { get; set; } = "https://api.igdb.com/v4";
}
