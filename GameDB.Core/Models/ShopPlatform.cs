namespace GameDB.Core.Models;

/// <summary>
/// Represents the supported external game shop platforms for wishlist import and price synchronization.
/// </summary>
public enum ShopPlatform
{
    /// <summary>Steam platform (Valve Corporation).</summary>
    Steam = 1,

    /// <summary>GOG.com platform (CD Projekt).</summary>
    GOG = 2,

    /// <summary>Epic Games Store platform (Epic Games).</summary>
    EpicGames = 3
}
