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
    private readonly ShopLinkService _shopLink;
    private readonly ILogger<WishlistService> _logger;

    public WishlistService(AppDbContext db, IHttpClientFactory httpFactory, ShopLinkService shopLink, ILogger<WishlistService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _shopLink = shopLink;
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
            if (result is not bool added)
                throw new InvalidOperationException("fn_toggle_wishlist did not return a boolean value.");
            
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
    /// Requires user to have linked their shop account (ExternalUid in UserShopProfile).
    /// </summary>
    public async Task<(int imported, string? error)> ImportAsync(int userId, int shopId)
    {
        var profile = await _shopLink.GetProfileAsync(userId, shopId);
        if (profile == null || string.IsNullOrWhiteSpace(profile.ExternalUid))
            return (0, $"{ShopLinkService.ShopIdToSlug(shopId)?.ToUpper()} account not linked. Please link your account first.");

        var externalId = profile.ExternalUid.Trim();
        if (shopId == 1 && externalId.Contains('/'))
            externalId = externalId.Substring(externalId.LastIndexOf('/') + 1);

        return shopId switch
        {
            1 => await ImportSteamAsync(userId, externalId),
            2 => await ImportGogAsync(userId, externalId),
            _ => (0, $"Unknown shop ID: {shopId}")
        };
    }

    /// <summary>
    /// Import wishlist from Steam using user's Steam64 ID.

    /// </summary>
    public async Task<(int imported, string? error)> ImportSteamAsync(int userId, string steamId)
    {
        try
        {
            var client = _httpFactory.CreateClient();
            var url = $"https://api.steampowered.com/IWishlistService/GetWishlist/v1/?steamid={steamId}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "GameDB/1.0");

            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Steam wishlist API returned {StatusCode} for steamId {SteamId}, content length {Length}",
                (int)response.StatusCode, steamId, content?.Length ?? 0);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Steam wishlist API error: {Content}", content);
                return (0, "Could not access your Steam wishlist. Make sure your wishlist is public.");
            }

            if (string.IsNullOrWhiteSpace(content))
                return (0, "Steam returned an empty response.");

            if (content.TrimStart().StartsWith("<"))
                return (0, "Steam returned an HTML page instead of data. Your wishlist may be private.");

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(content);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Steam wishlist response");
                return (0, "Invalid response from Steam. Please check your Steam ID.");
            }

            var appIds = new List<string>();

            if (doc.RootElement.TryGetProperty("response", out var responseObj) &&
                responseObj.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("appid", out var appid))
                    {
                        var idStr = appid.ValueKind == JsonValueKind.Number
                            ? appid.GetUInt32().ToString()
                            : appid.GetString() ?? "";
                        if (!string.IsNullOrEmpty(idStr))
                            appIds.Add(idStr);
                    }
                }
            }
            else
            {
                appIds = doc.RootElement.EnumerateObject()
                    .Select(p => p.Name)
                    .Where(name => uint.TryParse(name, out _))
                    .ToList();
            }

            _logger.LogInformation("Steam wishlist: found {Count} app IDs for user {UserId}", appIds.Count, userId);

            if (appIds.Count == 0)
                return (0, "Your Steam wishlist is empty or could not be read.");

            _logger.LogDebug("Steam app IDs: {AppIds}", string.Join(", ", appIds.Take(20)));

            var matchedGameIds = await _db.GameOffers
                .Where(o => o.ShopId == 1 && o.ExternalId != null && appIds.Contains(o.ExternalId))
                .Select(o => o.GameId)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation("Matched {Count} games from Steam wishlist to catalog (out of {Total} app IDs)",
                matchedGameIds.Count, appIds.Count);

            return await AddImportedGamesAsync(userId, 1, matchedGameIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Steam wishlist import failed for user {UserId}", userId);
            return (0, $"Steam import failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Import wishlist from GOG using the public JSON API.
    /// Endpoint: GET https://www.gog.com/u/{username}/wishlist/games/json
    /// Returns an array of objects with numeric "id" field — no auth needed for public wishlists.
    /// </summary>
    public async Task<(int imported, string? error)> ImportGogAsync(int userId, string gogUsername)
    {
        try
        {
            var client = _httpFactory.CreateClient();

            // This is the correct GOG public JSON endpoint — /wishlist (without /games/json)
            // returns HTML (Angular SPA), which is unparseable via regex.
            var url = $"https://www.gog.com/u/{Uri.EscapeDataString(gogUsername)}/wishlist/games/json";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Referer", "https://www.gog.com/");

            var response = await client.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return (0, "GOG profile not found. Double-check your GOG username.");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GOG wishlist API returned {StatusCode} for user {GogUser}",
                    (int)response.StatusCode, gogUsername);
                return (0, "Could not access your GOG wishlist. Check your username and make sure your wishlist is public.");
            }

            var content = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(content))
                return (0, "GOG returned an empty response.");

            List<string> productIds;

            try
            {
                using var doc = JsonDocument.Parse(content);
                productIds = ParseGogProductIds(doc.RootElement);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "GOG wishlist JSON parse error for user {GogUser}", gogUsername);
                return (0, "GOG returned an unexpected format. Please try again later.");
            }

            _logger.LogInformation("GOG wishlist: found {Count} product IDs for user {GogUser}",
                productIds.Count, gogUsername);

            if (productIds.Count == 0)
                return (0, "Your GOG wishlist is empty or could not be parsed. Make sure your wishlist is public.");

            var matchedGameIds = await _db.GameOffers
                .Where(o => o.ShopId == 2 && o.ExternalId != null && productIds.Contains(o.ExternalId))
                .Select(o => o.GameId)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation("Matched {Count} games from GOG wishlist to catalog (out of {Total} product IDs)",
                matchedGameIds.Count, productIds.Count);

            return await AddImportedGamesAsync(userId, 2, matchedGameIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GOG wishlist import failed for user {UserId}", userId);
            return (0, $"GOG import failed: {ex.Message}");
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

        _logger.LogInformation("User {UserId} imported {Count} games from shop {ShopId}",
            userId, totalAffected, shopId);

        return (totalAffected, newGameIds.Count < gameIds.Count
            ? $"{gameIds.Count - newGameIds.Count} games were already in your wishlist"
            : null);
    }

    /// <summary>
    /// Parses GOG product IDs from a JsonElement.
    /// GOG /wishlist/games/json returns an array of objects with a numeric "id" field.
    /// Fallback: object with numeric top-level keys (legacy format).
    /// </summary>
    private static List<string> ParseGogProductIds(JsonElement root)
    {
        var ids = new List<string>();

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                string? id = null;

                if (item.TryGetProperty("id", out var idProp))
                    id = idProp.ValueKind == JsonValueKind.Number
                        ? idProp.GetInt64().ToString()
                        : idProp.GetString();
                else if (item.TryGetProperty("productId", out var pid))
                    id = pid.ValueKind == JsonValueKind.Number
                        ? pid.GetInt64().ToString()
                        : pid.GetString();

                if (!string.IsNullOrEmpty(id))
                    ids.Add(id);
            }
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            // Legacy: numeric keys at root level
            ids = root.EnumerateObject()
                .Select(p => p.Name)
                .Where(name => long.TryParse(name, out _))
                .ToList();
        }

        return ids;
    }
}