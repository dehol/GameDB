using GameDB.Core.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;

namespace GameDB.Infrastructure.Services;

public class LibraryService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ShopLinkService _shopLink;
    private readonly ILogger<LibraryService> _logger;

    public LibraryService(AppDbContext db, IHttpClientFactory httpFactory, ShopLinkService shopLink, ILogger<LibraryService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _shopLink = shopLink;
        _logger = logger;
    }

    public async Task<List<UserLibraryRow>> GetLibraryAsync(int userId)
    {
        return await _db.Database
            .SqlQueryRaw<UserLibraryRow>("SELECT * FROM vw_user_library WHERE \"UserId\" = {0}", userId)
            .ToListAsync();
    }

    public async Task<string?> AddToLibraryAsync(int userId, int gameId, int shopId)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "CALL pr_add_to_library({0}, {1}, {2})",
                userId, gameId, shopId);
            return null;
        }
        catch (PostgresException ex)
        {
            return ex.MessageText;
        }
    }

    /// <summary>
    /// Import library from a specific shop.
    /// Steam: GetOwnedGames API with Steam64 ID.
    /// GOG: public /games/json endpoint with GOG username.
    /// itch.io: official API with user-provided API key.
    /// </summary>
    public async Task<(int imported, string? error)> ImportAsync(int userId, int shopId)
    {
        var profile = await _shopLink.GetProfileAsync(userId, shopId);
        if (profile == null || string.IsNullOrWhiteSpace(profile.ExternalUid))
            return (0, $"{ShopLinkService.ShopIdToSlug(shopId)?.ToUpper()} account not linked.");

        var externalId = profile.ExternalUid.Trim();
        if (shopId == 1 && externalId.Contains('/'))
            externalId = externalId.Substring(externalId.LastIndexOf('/') + 1);

        return shopId switch
        {
            1 => await ImportSteamLibraryAsync(userId, externalId),
            2 => await ImportGogLibraryAsync(userId, externalId),
            _ => (0, $"Unknown shop ID: {shopId}")
        };
    }

    /// <summary>
    /// Import Steam library using GetOwnedGames API.
    /// Requires the user's Steam profile and game details to be set to public.
    /// </summary>
    private async Task<(int imported, string? error)> ImportSteamLibraryAsync(int userId, string steamId)
    {
        try
        {
            var client = _httpFactory.CreateClient();

            var url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/" +
                      $"?steamid={steamId}&include_appids=1&include_played_free_games=1";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "GameDB/1.0");

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Steam library API returned {StatusCode}", (int)response.StatusCode);
                return (0, "Could not access your Steam library. Make sure your profile and game details are public.");
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            var appIds = new List<string>();

            if (doc.RootElement.TryGetProperty("response", out var responseObj) &&
                responseObj.TryGetProperty("games", out var games))
            {
                foreach (var game in games.EnumerateArray())
                {
                    if (game.TryGetProperty("appid", out var appid))
                        appIds.Add(appid.GetUInt32().ToString());
                }
            }

            if (appIds.Count == 0)
                return (0, "Your Steam library is empty or could not be read. Make sure your game details are public.");

            var matchedGameIds = await _db.GameOffers
                .Where(o => o.ShopId == 1 && o.ExternalId != null && appIds.Contains(o.ExternalId))
                .Select(o => o.GameId)
                .Distinct()
                .ToListAsync();

            return await AddImportedLibraryGamesAsync(userId, 1, matchedGameIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Steam library import failed for user {UserId}", userId);
            return (0, $"Steam library import failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Import GOG library using the public JSON endpoint.
    /// Endpoint: GET https://www.gog.com/u/{username}/games/json
    /// Returns an array of objects with numeric "id" field — no auth needed for public profiles.
    /// The user must set their game collection to public in GOG profile settings.
    /// </summary>
    private async Task<(int imported, string? error)> ImportGogLibraryAsync(int userId, string gogUsername)
    {
        try
        {
            var client = _httpFactory.CreateClient();

            // /games/json is the correct endpoint — /games alone returns HTML (Angular SPA)
            var url = $"https://www.gog.com/u/{Uri.EscapeDataString(gogUsername)}/games/json";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Referer", "https://www.gog.com/");

            var response = await client.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return (0, "GOG profile not found. Double-check your GOG username.");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GOG library API returned {StatusCode} for user {GogUser}",
                    (int)response.StatusCode, gogUsername);
                return (0, "Could not access your GOG library. Check your username and make sure your games are visible on your profile.");
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
                _logger.LogWarning(ex, "GOG library JSON parse error for user {GogUser}", gogUsername);
                return (0, "GOG returned an unexpected format. Please try again later.");
            }

            _logger.LogInformation("GOG library: found {Count} product IDs for user {GogUser}",
                productIds.Count, gogUsername);

            if (productIds.Count == 0)
                return (0, "Your GOG library is empty or could not be parsed. Make sure your games are visible on your profile.");

            var matchedGameIds = await _db.GameOffers
                .Where(o => o.ShopId == 2 && o.ExternalId != null && productIds.Contains(o.ExternalId))
                .Select(o => o.GameId)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation("Matched {Count} games from GOG library to catalog (out of {Total} product IDs)",
                matchedGameIds.Count, productIds.Count);

            return await AddImportedLibraryGamesAsync(userId, 2, matchedGameIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GOG library import failed for user {UserId}", userId);
            return (0, $"GOG library import failed: {ex.Message}");
        }
    }

    private async Task<(int imported, string? error)> AddImportedLibraryGamesAsync(int userId, int shopId, List<int> gameIds)
    {
        if (gameIds.Count == 0)
            return (0, "No matching games found in our catalog from your library.");

        var existingGameIds = (await _db.UserLibraries
            .Where(l => l.UserId == userId && gameIds.Contains(l.GameId) && l.ShopId == shopId)
            .Select(l => l.GameId)
            .ToListAsync()).ToHashSet();

        var newGameIds = gameIds.Except(existingGameIds).ToList();

        foreach (var gameId in newGameIds)
        {
            _db.UserLibraries.Add(new Core.Models.UserLibrary
            {
                UserId = userId,
                GameId = gameId,
                ShopId = shopId
            });
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} imported {Count} library games from shop {ShopId}",
            userId, newGameIds.Count, shopId);

        return (newGameIds.Count, existingGameIds.Count > 0
            ? $"{existingGameIds.Count} games were already in your library"
            : null);
    }

    /// <summary>
    /// Parses GOG product IDs from a JsonElement.
    /// GOG JSON endpoints return an array of objects with a numeric "id" field.
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
            ids = root.EnumerateObject()
                .Select(p => p.Name)
                .Where(name => long.TryParse(name, out _))
                .ToList();
        }

        return ids;
    }
}