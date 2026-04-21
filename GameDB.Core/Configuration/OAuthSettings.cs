namespace GameDB.Core.Configuration;

public class OAuthSettings
{
    public OAuthShopConfig Steam { get; set; } = new();
    public OAuthShopConfig Gog { get; set; } = new();
    public OAuthShopConfig Egs { get; set; } = new();
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
}

public class OAuthShopConfig
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AuthorizeUrl { get; set; } = "";
    public string TokenUrl { get; set; } = "";
    public string Scope { get; set; } = "";
    public string RedirectPath { get; set; } = "";
}
