using GameDB.Core.DTOs;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GameDB.Infrastructure.Services;

public class WishlistService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ShopOAuthService _oauth;
    private readonly ILogger<WishlistService> _logger;

    public WishlistService(AppDbContext db, IHttpClientFactory httpFactory, ShopOAuthService oauth, ILogger<WishlistService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _oauth = oauth;
        _logger = logger;
    }

    public async Task<List<WishlistItemDto>> GetByUserAsync(int userId)
    {
        return await _db.Wishlists
            .Include(w => w.Sources).ThenInclude(s => s.Shop)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.AddedAt)
            .Select(w => new WishlistItemDto(
                w.GameId,
                w.Game.Title,
                w.AddedAt,
                w.Sources.Select(s => s.Shop.Name).OrderBy(n => n).ToList()
            ))
            .ToListAsync();
    }

    public async Task<(bool success, string? error)> AddAsync(int userId, int gameId)
    {
        if (!await _db.Games.AnyAsync(g => g.GameId == gameId))
            return (false, "Game not found");

        if (await _db.Wishlists.AnyAsync(w => w.UserId == userId && w.GameId == gameId))
            return (false, "Game already in wishlist");

        _db.Wishlists.Add(new Wishlist { UserId = userId, GameId = gameId });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> RemoveAsync(int userId, int gameId)
    {
        var entry = await _db.Wishlists.FindAsync(userId, gameId);
        if (entry == null) return false;
        _db.Wishlists.Remove(entry);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Toggle wishlist item using raw SQL (atomic operation)
    /// </summary>
    public async Task<(bool added, string message)> ToggleAsync(int userId, int gameId)
    {
        if (!await _db.Games.AnyAsync(g => g.GameId == gameId))
            return (false, "Game not found");

        var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT fn_toggle_wishlist(@userId, @gameId)";

            var p1 = cmd.CreateParameter();
            p1.ParameterName = "userId";
            p1.Value = userId;
            cmd.Parameters.Add(p1);

            var p2 = cmd.CreateParameter();
            p2.ParameterName = "gameId";
            p2.Value = gameId;
            cmd.Parameters.Add(p2);

            var result = await cmd.ExecuteScalarAsync();
            var added = result != null && (bool)result;

            return added
                ? (true, "Added to wishlist")
                : (false, "Removed from wishlist");
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Universal import method — routes to shop-specific import logic.
    /// </summary>
    public async Task<(int imported, string? error)> ImportAsync(int userId, int shopId)
    {
        return shopId switch
        {
            1 => await ImportSteamAsync(userId),
            2 => await ImportGogAsync(userId),
            3 => await ImportEgsAsync(userId),
            _ => (0, $"Unknown shop ID: {shopId}")
        };
    }

    /// <summary>
    /// Import wishlist from Steam using user's linked Steam profile.
    /// </summary>
    public async Task<(int imported, string? error)> ImportSteamAsync(int userId)
    {
        var profile = await _db.UserShopProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ShopId == 1);

        if (profile == null)
            return (0, "Steam account not linked. Please link your Steam account first.");

        var steamId = profile.ExternalUid;

        try
        {
            var client = _httpFactory.CreateClient();

            var urls = new[]
            {
                $"https://api.steampowered.com/IWishlistService/GetWishlist/v1/?steamid={steamId}",
                $"https://store.steampowered.com/wishlist/profiles/{steamId}/wishlistdata/?p=0",
                $"https://store.steampowered.com/wishlist/id/{steamId}/wishlistdata/?p=0",
            };

            string? content = null;
            HttpResponseMessage? response = null;

            foreach (var url in urls)
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    request.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
                    request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                    request.Headers.Add("Referer", "https://store.steampowered.com/");

                    response = await client.SendAsync(request);
                    content = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode && !content.TrimStart().StartsWith("<"))
                        break;
                }
                catch { continue; }
            }

            if (response == null || !response.IsSuccessStatusCode)
                return (0, "Could not connect to Steam. Please try again later.");

            if (string.IsNullOrWhiteSpace(content))
                return (0, "Steam returned an empty response. Please try again later.");

            if (content.TrimStart().StartsWith("<"))
                return (0, "Steam wishlist is private or Steam ID is invalid. Please check your account and ensure your wishlist is public.");

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(content);
            }
            catch
            {
                return (0, "Invalid response from Steam. Please check your Steam account.");
            }

            var appIds = new List<string>();

            if (doc.RootElement.TryGetProperty("response", out var responseObj) &&
                responseObj.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("appid", out var appid))
                        appIds.Add(appid.GetUInt32().ToString());
                }
            }
            else
            {
                appIds = doc.RootElement.EnumerateObject()
                    .Select(p => p.Name)
                    .Where(name => uint.TryParse(name, out _))
                    .ToList();
            }

            if (appIds.Count == 0)
                return (0, "Your Steam wishlist is empty or could not be read.");

            var matchedGameIds = await _db.GameOffers
                .Where(o => o.ShopId == 1 && o.ExternalId != null && appIds.Contains(o.ExternalId))
                .Select(o => o.GameId)
                .Distinct()
                .ToListAsync();

            return await AddImportedGamesAsync(userId, 1, matchedGameIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Steam wishlist import failed for user {UserId}", userId);
            return (0, $"Steam import failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Import wishlist from GOG using user's OAuth token.
    /// </summary>
    public async Task<(int imported, string? error)> ImportGogAsync(int userId)
    {
        var profile = await _db.UserShopProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ShopId == 2);

        if (profile == null || string.IsNullOrWhiteSpace(profile.AccessToken))
            return (0, "GOG account not linked. Please link your GOG account first.");

        try
        {
            var client = _httpFactory.CreateClient();

            // GOG wishlist endpoints in priority order.
            // www.gog.com/user/wishlist.json returns { "wishlist": { "<productId>": true, ... }, "checksum": "..." }
            var urls = new[]
            {
                "https://www.gog.com/user/wishlist.json",
                "https://www.gog.com/user/data.json",
            };

            List<string> productIds = new();

            foreach (var url in urls)
            {
                // profile can be reassigned inside the loop (after token refresh); skip if no longer valid
                if (profile == null || string.IsNullOrWhiteSpace(profile.AccessToken))
                    break;

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", profile.AccessToken);
                    request.Headers.Add("User-Agent", "GameDB/1.0");

                    var response = await client.SendAsync(request);

                    // On 401, attempt a token refresh once and retry
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _logger.LogWarning("GOG wishlist API returned 401 from {Url}, attempting token refresh", url);
                        var (refreshed, _) = await _oauth.RefreshTokenAsync(userId, 2);
                        if (refreshed)
                        {
                            // Re-read the updated token from DB
                            profile = await _db.UserShopProfiles
                                .FirstOrDefaultAsync(p => p.UserId == userId && p.ShopId == 2);
                            if (profile?.AccessToken != null)
                            {
                                var retryRequest = new HttpRequestMessage(HttpMethod.Get, url);
                                retryRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", profile.AccessToken);
                                retryRequest.Headers.Add("User-Agent", "GameDB/1.0");
                                response = await client.SendAsync(retryRequest);
                            }
                        }
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("GOG wishlist API returned {StatusCode} from {Url}", (int)response.StatusCode, url);
                        continue;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);

                    var parsed = new List<string>();

                    // GOG wishlist format: array of objects with "id" or object with numeric keys
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            var id = item.TryGetProperty("id", out var idProp) ? idProp.ToString() :
                                     item.TryGetProperty("productId", out var pidProp) ? pidProp.ToString() : null;
                            if (id != null)
                                parsed.Add(id);
                        }
                    }
                    else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        // Check for wishlist property (format: { "wishlist": { "123": true }, "checksum": "..." })
                        if (doc.RootElement.TryGetProperty("wishlist", out var wishlist))
                        {
                            if (wishlist.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in wishlist.EnumerateArray())
                                {
                                    var id = item.TryGetProperty("id", out var idProp) ? idProp.ToString() :
                                             item.TryGetProperty("productId", out var pidProp) ? pidProp.ToString() : null;
                                    if (id != null)
                                        parsed.Add(id);
                                }
                            }
                            else if (wishlist.ValueKind == JsonValueKind.Object)
                            {
                                // Object with numeric product ID keys mapping to boolean values
                                parsed = wishlist.EnumerateObject()
                                    .Select(p => p.Name)
                                    .Where(name => long.TryParse(name, out _))
                                    .ToList();
                            }
                        }
                        else
                        {
                            // Object with numeric keys at root level (direct format)
                            parsed = doc.RootElement.EnumerateObject()
                                .Select(p => p.Name)
                                .Where(name => long.TryParse(name, out _))
                                .ToList();
                        }
                    }

                    // Only accept result from this URL if we got IDs; otherwise try next URL
                    if (parsed.Count > 0)
                    {
                        productIds = parsed;
                        break;
                    }

                    _logger.LogWarning("GOG wishlist from {Url} returned 0 parseable product IDs, trying next URL", url);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch GOG wishlist from {Url}", url);
                    continue;
                }
            }

            if (productIds.Count == 0)
                return (0, "Your GOG wishlist is empty or could not be read. Make sure your GOG account is linked correctly.");

            var matchedGameIds = await _db.GameOffers
                .Where(o => o.ShopId == 2 && o.ExternalId != null && productIds.Contains(o.ExternalId))
                .Select(o => o.GameId)
                .Distinct()
                .ToListAsync();

            return await AddImportedGamesAsync(userId, 2, matchedGameIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GOG wishlist import failed for user {UserId}", userId);
            return (0, $"GOG import failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Import wishlist from Epic Games Store using user's OAuth token.
    /// </summary>
    public async Task<(int imported, string? error)> ImportEgsAsync(int userId)
    {
        var profile = await _db.UserShopProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ShopId == 3);

        if (profile == null || string.IsNullOrWhiteSpace(profile.AccessToken))
            return (0, "Epic Games account not linked. Please link your Epic Games account first.");

        try
        {
            var client = _httpFactory.CreateClient();

            // Epic Games Store GraphQL API — wishlist items expose offerId directly
            var graphqlQuery = new
            {
                query = @"query getWishlist { Wishlist { wishlistItems { offerId } } }"
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://graphql.epicgames.com/graphql");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", profile.AccessToken);
            request.Headers.Add("User-Agent", "GameDB/1.0");
            request.Content = new StringContent(JsonSerializer.Serialize(graphqlQuery), System.Text.Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("EGS wishlist API returned {StatusCode}: {Body}", (int)response.StatusCode, errorBody);
                return (0, "Could not access your Epic Games wishlist. Please try re-linking your account.");
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            var epicIds = new List<string>();

            // Parse GraphQL response: data.Wishlist.wishlistItems[].offerId
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("Wishlist", out var wishlist) &&
                wishlist.TryGetProperty("wishlistItems", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("offerId", out var offerIdProp))
                    {
                        var id = offerIdProp.GetString();
                        if (id != null)
                            epicIds.Add(id);
                    }
                }
            }

            if (epicIds.Count == 0)
                return (0, "Your Epic Games wishlist is empty or could not be read.");

            var matchedGameIds = await _db.GameOffers
                .Where(o => o.ShopId == 3 && o.ExternalId != null && epicIds.Contains(o.ExternalId))
                .Select(o => o.GameId)
                .Distinct()
                .ToListAsync();

            return await AddImportedGamesAsync(userId, 3, matchedGameIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EGS wishlist import failed for user {UserId}", userId);
            return (0, $"Epic Games import failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds matched games to wishlist with WishlistSource records.
    /// Skips games already in wishlist (only adds WishlistSource if missing).
    /// </summary>
    private async Task<(int imported, string? error)> AddImportedGamesAsync(int userId, int shopId, List<int> gameIds)
    {
        if (gameIds.Count == 0)
            return (0, "No matching games found in our catalog from your wishlist.");

        var existingWishlist = await _db.Wishlists
            .Include(w => w.Sources)
            .Where(w => w.UserId == userId && gameIds.Contains(w.GameId))
            .ToListAsync();

        var existingGameIds = existingWishlist.Select(w => w.GameId).ToHashSet();
        var newGameIds = gameIds.Except(existingGameIds).ToList();

        // Add new wishlist entries
        foreach (var gameId in newGameIds)
        {
            _db.Wishlists.Add(new Wishlist
            {
                UserId = userId,
                GameId = gameId,
                Sources = new List<WishlistSource>
                {
                    new() { UserId = userId, GameId = gameId, ShopId = shopId }
                }
            });
        }

        // Add WishlistSource for existing entries that don't have this shop yet
        foreach (var entry in existingWishlist)
        {
            if (!entry.Sources.Any(s => s.ShopId == shopId))
            {
                _db.WishlistSources.Add(new WishlistSource
                {
                    UserId = userId,
                    GameId = entry.GameId,
                    ShopId = shopId
                });
            }
        }

        await _db.SaveChangesAsync();

        var totalAffected = newGameIds.Count +
            existingWishlist.Count(w => !w.Sources.Any(s => s.ShopId == shopId));

        _logger.LogInformation("User {UserId} imported {Count} games from shop {ShopId}", userId, totalAffected, shopId);

        return (totalAffected, newGameIds.Count < gameIds.Count
            ? $"{gameIds.Count - newGameIds.Count} games were already in your wishlist"
            : null);
    }
}
