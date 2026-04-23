using GameDB.Core.Configuration;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace GameDB.Infrastructure.Services;

public class ShopOAuthService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly OAuthSettings _settings;
    private readonly ILogger<ShopOAuthService> _logger;

    // In-memory pending OAuth states (sufficient for single-server deployment)
    private static readonly Dictionary<string, (int UserId, int ShopId, DateTime CreatedAt)> _pendingStates = new();

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
        "gog" => 2,
        "egs" => 3,
        _ => null
    };

    public static string? ShopIdToSlug(int shopId) => shopId switch
    {
        1 => "steam",
        2 => "gog",
        3 => "egs",
        _ => null
    };

    /// <summary>
    /// Generates authorization URL for the specified shop and stores state for CSRF protection.
    /// </summary>
    public (string url, string state) GetAuthorizationUrl(int userId, int shopId)
    {
        var state = GenerateState(userId, shopId);
        return shopId switch
        {
            1 => BuildSteamAuthUrl(state),
            2 => BuildGogAuthUrl(state),
            3 => BuildEgsAuthUrl(state),
            _ => throw new ArgumentException($"Unknown shop ID: {shopId}")
        };
    }

    /// <summary>
    /// Handles OAuth callback — exchanges code for tokens, stores in UserShopProfile.
    /// For Steam, verifies OpenID response and extracts Steam64 ID.
    /// </summary>
    public async Task<(bool success, string? error)> HandleCallbackAsync(string state, string code)
    {
        // Validate and consume state
        if (!_pendingStates.TryGetValue(state, out var pending))
            return (false, "Invalid or expired OAuth state. Please try again.");

        _pendingStates.Remove(state);

        // State expires after 10 minutes
        if (DateTime.UtcNow - pending.CreatedAt > TimeSpan.FromMinutes(10))
            return (false, "OAuth state expired. Please try again.");

        return pending.ShopId switch
        {
            1 => await HandleSteamCallbackAsync(pending.UserId, code),
            2 => await HandleGogCallbackAsync(pending.UserId, code),
            3 => await HandleEgsCallbackAsync(pending.UserId, code),
            _ => (false, $"Unknown shop ID: {pending.ShopId}")
        };
    }

    /// <summary>
    /// Handles Steam OpenID callback — code contains the full query string with OpenID params.
    /// </summary>
    public async Task<(bool success, string? error)> HandleSteamCallbackAsync(int userId, string claimedId)
    {
        // Extract Steam64 ID from claimed_id (format: https://steamcommunity.com/openid/id/765611980XXXXXX)
        var steamId = claimedId;
        if (steamId.Contains('/'))
            steamId = steamId.Substring(steamId.LastIndexOf('/') + 1);

        if (!ulong.TryParse(steamId, out _))
            return (false, "Invalid Steam ID format.");

        // Store in UserShopProfile
        await UpsertProfileAsync(userId, 1, steamId, null, null, null);
        _logger.LogInformation("User {UserId} linked Steam account {SteamId}", userId, steamId);
        return (true, null);
    }

    /// <summary>
    /// Handles GOG OAuth callback — exchanges authorization code for tokens.
    /// </summary>
    public async Task<(bool success, string? error)> HandleGogCallbackAsync(int userId, string code)
    {
        var config = _settings.Gog;
        var client = _httpFactory.CreateClient();

        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, config.TokenUrl);
        var redirectUri = BuildRedirectUri(2);
        var body = $"grant_type=authorization_code&code={Uri.EscapeDataString(code)}" +
                   $"&client_id={Uri.EscapeDataString(config.ClientId)}" +
                   $"&client_secret={Uri.EscapeDataString(config.ClientSecret)}" +
                   $"&redirect_uri={Uri.EscapeDataString(redirectUri)}";
        tokenRequest.Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");

        var response = await client.SendAsync(tokenRequest);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("GOG token exchange failed: {StatusCode} - {Body}", (int)response.StatusCode, errorBody);
            return (false, "Failed to exchange GOG authorization code. Please try again.");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var accessToken = doc.RootElement.GetProperty("access_token").GetString()!;
        var refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;

        // Get GOG user ID
        var gogUserId = await GetGogUserIdAsync(accessToken);

        await UpsertProfileAsync(userId, 2, gogUserId ?? "gog_user", accessToken, refreshToken, DateTime.UtcNow.AddSeconds(expiresIn));
        _logger.LogInformation("User {UserId} linked GOG account", userId);
        return (true, null);
    }

    /// <summary>
    /// Handles Epic Games Store OAuth callback — exchanges authorization code for tokens.
    /// EGS requires client credentials as a Basic Authorization header, not in the request body.
    /// </summary>
    public async Task<(bool success, string? error)> HandleEgsCallbackAsync(int userId, string code)
    {
        var config = _settings.Egs;
        var client = _httpFactory.CreateClient();

        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, config.TokenUrl);
        var redirectUri = BuildRedirectUri(3);
        // EGS requires Basic auth (base64 clientId:clientSecret) instead of body credentials
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.ClientId}:{config.ClientSecret}"));
        tokenRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        var body = $"grant_type=authorization_code&code={Uri.EscapeDataString(code)}" +
                   $"&redirect_uri={Uri.EscapeDataString(redirectUri)}";
        tokenRequest.Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");

        var response = await client.SendAsync(tokenRequest);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("EGS token exchange failed: {StatusCode} - {Body}", (int)response.StatusCode, errorBody);
            return (false, "Failed to exchange Epic Games authorization code. Please try again.");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var accessToken = doc.RootElement.GetProperty("access_token").GetString()!;
        var refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;

        // Get Epic account ID from token
        var epicAccountId = doc.RootElement.TryGetProperty("account_id", out var aid) ? aid.GetString() ?? "epic_user" : "epic_user";

        await UpsertProfileAsync(userId, 3, epicAccountId, accessToken, refreshToken, DateTime.UtcNow.AddSeconds(expiresIn));
        _logger.LogInformation("User {UserId} linked Epic Games account", userId);
        return (true, null);
    }

    /// <summary>
    /// Refreshes the access token for the specified shop using the stored refresh token.
    /// </summary>
    public async Task<(bool success, string? error)> RefreshTokenAsync(int userId, int shopId)
    {
        var profile = await _db.UserShopProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ShopId == shopId);

        if (profile?.RefreshToken == null)
            return (false, "No refresh token available. Please re-link your account.");

        return shopId switch
        {
            2 => await RefreshGogTokenAsync(profile),
            3 => await RefreshEgsTokenAsync(profile),
            _ => (false, "Token refresh not supported for this shop.")
        };
    }

    /// <summary>
    /// Checks if user has valid authentication for the specified shop.
    /// For Steam: checks if UserShopProfile exists with ExternalUid.
    /// For GOG/EGS: checks if access token exists and is not expired (or refreshable).
    /// </summary>
    public async Task<bool> HasValidAuthAsync(int userId, int shopId)
    {
        var profile = await _db.UserShopProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ShopId == shopId);

        if (profile == null)
            return false;

        // Steam only needs ExternalUid
        if (shopId == 1)
            return !string.IsNullOrWhiteSpace(profile.ExternalUid);

        // GOG/EGS need a valid or refreshable token
        if (string.IsNullOrWhiteSpace(profile.AccessToken))
            return false;

        // If token is expired but we have a refresh token, try to refresh
        if (profile.TokenExpiresAt.HasValue && profile.TokenExpiresAt.Value < DateTime.UtcNow)
        {
            if (!string.IsNullOrWhiteSpace(profile.RefreshToken))
            {
                var (success, _) = await RefreshTokenAsync(userId, shopId);
                return success;
            }
            return false;
        }

        return true;
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

    private string GenerateState(int userId, int shopId)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var state = Convert.ToHexString(bytes).ToLowerInvariant();
        _pendingStates[state] = (userId, shopId, DateTime.UtcNow);

        // Clean up expired states
        var expired = _pendingStates
            .Where(kv => DateTime.UtcNow - kv.Value.CreatedAt > TimeSpan.FromMinutes(10))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in expired)
            _pendingStates.Remove(key);

        return state;
    }

    private (string url, string state) BuildSteamAuthUrl(string state)
    {
        var redirectUri = BuildRedirectUri(1);
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

    private (string url, string state) BuildGogAuthUrl(string state)
    {
        var config = _settings.Gog;
        var redirectUri = BuildRedirectUri(2);
        var queryParams = HttpUtility.ParseQueryString(string.Empty);
        queryParams["client_id"] = config.ClientId;
        queryParams["redirect_uri"] = redirectUri;
        queryParams["response_type"] = "code";
        queryParams["state"] = state;
        if (!string.IsNullOrWhiteSpace(config.Scope))
            queryParams["scope"] = config.Scope;

        var url = $"{config.AuthorizeUrl}?{queryParams}";
        return (url, state);
    }

    private (string url, string state) BuildEgsAuthUrl(string state)
    {
        var config = _settings.Egs;
        var redirectUri = BuildRedirectUri(3);
        var queryParams = HttpUtility.ParseQueryString(string.Empty);
        queryParams["client_id"] = config.ClientId;
        queryParams["redirect_uri"] = redirectUri;
        queryParams["response_type"] = "code";
        queryParams["state"] = state;
        if (!string.IsNullOrWhiteSpace(config.Scope))
            queryParams["scope"] = config.Scope;

        var url = $"{config.AuthorizeUrl}?{queryParams}";
        return (url, state);
    }

    private string BuildRedirectUri(int shopId)
    {
        var slug = ShopIdToSlug(shopId) ?? "unknown";
        // Backend callback URL
        return $"http://localhost:5212/api/oauth/{slug}/callback";
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

    private async Task<string?> GetGogUserIdAsync(string accessToken)
    {
        try
        {
            var client = _httpFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, "https://embed.gog.com/userData.json");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("userId", out var uid) ? uid.GetString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get GOG user ID");
            return null;
        }
    }

    private async Task<(bool success, string? error)> RefreshGogTokenAsync(UserShopProfile profile)
    {
        var config = _settings.Gog;
        var client = _httpFactory.CreateClient();

        var body = $"grant_type=refresh_token&refresh_token={Uri.EscapeDataString(profile.RefreshToken!)}" +
                   $"&client_id={Uri.EscapeDataString(config.ClientId)}" +
                   $"&client_secret={Uri.EscapeDataString(config.ClientSecret)}";

        var request = new HttpRequestMessage(HttpMethod.Post, config.TokenUrl);
        request.Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GOG token refresh failed: {StatusCode}", (int)response.StatusCode);
            return (false, "Failed to refresh GOG token. Please re-link your account.");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        profile.AccessToken = doc.RootElement.GetProperty("access_token").GetString()!;
        if (doc.RootElement.TryGetProperty("refresh_token", out var rt))
            profile.RefreshToken = rt.GetString();
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;
        profile.TokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

        await _db.SaveChangesAsync();
        return (true, null);
    }

    private async Task<(bool success, string? error)> RefreshEgsTokenAsync(UserShopProfile profile)
    {
        var config = _settings.Egs;
        var client = _httpFactory.CreateClient();

        // EGS requires client credentials as Basic auth header, not in the body
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.ClientId}:{config.ClientSecret}"));
        var body = $"grant_type=refresh_token&refresh_token={Uri.EscapeDataString(profile.RefreshToken!)}";

        var request = new HttpRequestMessage(HttpMethod.Post, config.TokenUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        request.Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("EGS token refresh failed: {StatusCode}", (int)response.StatusCode);
            return (false, "Failed to refresh Epic Games token. Please re-link your account.");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        profile.AccessToken = doc.RootElement.GetProperty("access_token").GetString()!;
        if (doc.RootElement.TryGetProperty("refresh_token", out var rt))
            profile.RefreshToken = rt.GetString();
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;
        profile.TokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

        await _db.SaveChangesAsync();
        return (true, null);
    }

    #endregion
}
