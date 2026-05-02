namespace GameDB.Core.Constants;

/// <summary>
/// Constants for game store identification
/// </summary>
public static class ShopConstants
{
    public const int Steam = 1;
    public const int Gog = 2;
    
    public static string GetStoreUrl(int shopId, string externalId) => shopId switch
    {
        Steam => $"https://store.steampowered.com/app/{externalId}",
        Gog => $"https://www.gog.com/game/{externalId}",
        _ => string.Empty
    };
    
    public static string GetName(int shopId) => shopId switch
    {
        Steam => "Steam",
        Gog => "GOG",
        _ => "Unknown"
    };
}
