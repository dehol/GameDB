using GameDB.Core.Configuration;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Web;

namespace GameDB.Infrastructure.Services;

public class ShopOAuthService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly OAuthSettings _settings;
    private readonly ILogger<ShopOAuthService> _logger;

    private static readonly Dictionary<string, (int UserId, DateTime CreatedAt)> _pendingStates = new();

    public ShopOAuthService(
        AppDbContext db,
        IHttpClientFactory httpFactory,
        OAuthSettings settings,
        ILogger<ShopOAuthService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _settings = settings;
        _logger = logger;
    }

    public static int? ShopSlugToId(string slug) => slug.ToLowerInvariant() switch
    {
        "steam" => 1,
        "gog"   => 2,
        "itch"  => 3,   // 
        _ => null
    };

    public static string? ShopIdToSlug(int shopId) => shopId switch
    {
        1 => "steam",
        2 => "gog",
        3 => "itch",    // 
        _ => null
    };

    /// <summary>
    /// Links a shop account by external ID directly.
    /// Steam: Steam64 numeric ID.
    /// GOG: public GOG username.
    /// itch.io: personal API key (generated at itch.io → Settings → API keys).
    /// </summary>
    public async Task<(bool success, string? error)> LinkByExternalIdAsync(int userId, int shopId, string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return (false, "External ID cannot be empty.");

        externalId = externalId.Trim();

        switch (shopId)
        {
            case 1: // Steam — Steam64 numeric ID
                if (!ulong.TryParse(externalId, out _))
                    return (false, "Steam ID must be a numeric Steam64 ID (e.g. 76561198...).");
                break;

            case 2: // GOG — public username
                if (externalId.Length < 2 || externalId.Length > 64)
                    return (false, "GOG username must be between 2 and 64 characters.");
                break;

            case 3: // itch.io — personal API key
                if (externalId.Length < 10)
                    return (false, "itch.io API key looks too short. Generate one at itch.io → Settings → API keys.");
                break;

            default:
                return (false, $"Unknown shop ID: {shopId}");
        }

        await UpsertProfileAsync(userId, shopId, externalId, null, null, null);
        _logger.LogInformation("User {UserId} linked {Shop} account: {ExternalId}",
            userId, ShopIdToSlug(shopId), externalId);
        return (true, null);
    }

    /// <summary>
    /// Generates Steam OpenID authorization URL.
    /// </summary>
    public (string url, string state) GetSteamAuthUrl(int userId)
    {
        var state = GenerateState(userId);
        var redirectUri = BuildSteamRedirectUri();
        var queryParams = HttpUtility.ParseQueryString(string.Empty);
        queryParams["openid.ns"] = "http://specs.openid.net/auth/2.0";
        queryParams["openid.mode"] = "checkid_setup";
        queryParams["openid.return_to"] = redirectUri + "?state=" + state;
        queryParams["openid.realm"] = redirectUri;
        queryParams["openid.identity"] = "http://specs.openid.net/auth/2.0/identifier_select";
        queryParams["openid.claimed_id"] = "http://specs.openid.net/auth/2.0/identifier_select";

        var url = $"https://steamcommunity.com/openid/login?{queryParams}";
        return (url, state);
    }

    /// <summary>
    /// Handles Steam OpenID callback — extracts Steam64 ID from claimed_id.
    /// </summary>
    public async Task<(bool success, string? error)> HandleSteamCallbackAsync(string state, string claimedId)
    {
        if (!_pendingStates.TryGetValue(state, out var pending))
            return (false, "Invalid or expired OAuth state. Please try again.");

        _pendingStates.Remove(state);

        if (DateTime.UtcNow - pending.CreatedAt > TimeSpan.FromMinutes(10))
            return (false, "OAuth state expired. Please try again.");

        var steamId = claimedId;
        if (steamId.Contains('/'))
            steamId = steamId.Substring(steamId.LastIndexOf('/') + 1);

        if (!ulong.TryParse(steamId, out _))
            return (false, "Invalid Steam ID format.");

        await UpsertProfileAsync(pending.UserId, 1, steamId, null, null, null);
        _logger.LogInformation("User {UserId} linked Steam account {SteamId}", pending.UserId, steamId);
        return (true, null);
    }

    /// <summary>
    /// Checks if user has linked a shop account (has ExternalUid).
    /// </summary>
    public async Task<bool> IsLinkedAsync(int userId, int shopId)
    {
        return await _db.UserShopProfiles
            .AnyAsync(p => p.UserId == userId && p.ShopId == shopId && p.ExternalUid != null);
    }

    /// <summary>
    /// Gets the UserShopProfile for a specific user and shop.
    /// </summary>
    public async Task<UserShopProfile?> GetProfileAsync(int userId, int shopId)
    {
        return await _db.UserShopProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ShopId == shopId);
    }

    /// <summary>
    /// Removes the UserShopProfile (unlinks shop account).
    /// </summary>
    public async Task<bool> UnlinkAsync(int userId, int shopId)
    {
        var profile = await _db.UserShopProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ShopId == shopId);

        if (profile == null)
            return false;

        _db.UserShopProfiles.Remove(profile);
        await _db.SaveChangesAsync();
        _logger.LogInformation("User {UserId} unlinked shop {ShopId}", userId, shopId);
        return true;
    }

    #region Private helpers

    private string GenerateState(int userId)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var state = Convert.ToHexString(bytes).ToLowerInvariant();
        _pendingStates[state] = (userId, DateTime.UtcNow);

        var expired = _pendingStates
            .Where(kv => DateTime.UtcNow - kv.Value.CreatedAt > TimeSpan.FromMinutes(10))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in expired)
            _pendingStates.Remove(key);

        return state;
    }

    private string BuildSteamRedirectUri()
    {
        return $"http://localhost:5212/api/oauth/steam/callback";
    }

    private async Task UpsertProfileAsync(int userId, int shopId, string externalUid,
        string? accessToken, string? refreshToken, DateTime? tokenExpiresAt)
    {
        var existing = await _db.UserShopProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ShopId == shopId);

        if (existing != null)
        {
            existing.ExternalUid = externalUid;
            existing.AccessToken = accessToken;
            existing.RefreshToken = refreshToken;
            existing.TokenExpiresAt = tokenExpiresAt;
            existing.LinkedAt = DateTime.UtcNow;
        }
        else
        {
            _db.UserShopProfiles.Add(new UserShopProfile
            {
                UserId = userId,
                ShopId = shopId,
                ExternalUid = externalUid,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenExpiresAt = tokenExpiresAt
            });
        }

        await _db.SaveChangesAsync();
    }

    #endregion
}