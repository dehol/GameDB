namespace GameDB.Core.Configuration;

public class IgdbSettings
{
    public string ClientId { get; set; } = "5tbrr5lisv5uomqf69f7tu35qqpguv";
    public string ClientSecret { get; set; } = "dt478ha862bp63oo9xs832wptp6fnq";
    public string TokenUrl { get; set; } = "https://id.twitch.tv/oauth2/token";
    public string ApiBaseUrl { get; set; } = "https://api.igdb.com/v4";
}
