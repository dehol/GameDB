namespace GameDB.Core.Configuration;

/// <summary>
/// Top-level configuration for external shop API clients (Steam, GOG, Epic Games Store).
/// Bound from the <c>ShopApis</c> section in appsettings.json.
/// </summary>
public class ShopApiSettings
{
    /// <summary>Configuration specific to the Steam platform API.</summary>
    public SteamSettings Steam { get; set; } = new();

    /// <summary>Configuration specific to the GOG platform API.</summary>
    public GogSettings Gog { get; set; } = new();

    /// <summary>Configuration specific to the Epic Games Store API.</summary>
    public EpicSettings Epic { get; set; } = new();

    /// <summary>Steam platform API settings.</summary>
    public class SteamSettings
    {
        /// <summary>Base URL for the Steam Web API.</summary>
        public string ApiBaseUrl { get; set; } = "https://api.steampowered.com";

        /// <summary>Base URL for the Steam Store wishlist endpoints.</summary>
        public string WishlistUrl { get; set; } = "https://store.steampowered.com/wishlist";

        /// <summary>Maximum number of requests allowed per second to avoid rate-limiting.</summary>
        public int RateLimitPerSecond { get; set; } = 5;

        /// <summary>HTTP request timeout in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 30;
    }

    /// <summary>GOG platform API settings.</summary>
    public class GogSettings
    {
        /// <summary>Base URL for the GOG API.</summary>
        public string ApiBaseUrl { get; set; } = "https://api.gog.com";

        /// <summary>Maximum number of requests allowed per second to avoid rate-limiting.</summary>
        public int RateLimitPerSecond { get; set; } = 10;

        /// <summary>HTTP request timeout in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 30;
    }

    /// <summary>Epic Games Store API settings.</summary>
    public class EpicSettings
    {
        /// <summary>Base URL for the Epic Games launcher public service API.</summary>
        public string ApiBaseUrl { get; set; } = "https://launcher-public-service.epicgames.com";

        /// <summary>Maximum number of requests allowed per second to avoid rate-limiting.</summary>
        public int RateLimitPerSecond { get; set; } = 3;

        /// <summary>HTTP request timeout in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 30;
    }
}
