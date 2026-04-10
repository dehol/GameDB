namespace GameDB.Core.Configuration;

public class RawgSettings
{
    public string ApiKey { get; set; } = "994e337eac9f44838e18f1efe2c02aff";
    public string ApiBaseUrl { get; set; } = "https://api.rawg.io/api";
    public int RequestTimeoutSeconds { get; set; } = 60;
    public int PageSize { get; set; } = 40;
}
