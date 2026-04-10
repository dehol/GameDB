using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GameDB.Infrastructure.Services;

public class WishlistService : IWishlistService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;

    public WishlistService(AppDbContext db, IHttpClientFactory httpFactory)
    {
        _db = db;
        _httpFactory = httpFactory;
    }

    public async Task<List<WishlistItemDto>> GetByUserAsync(int userId)
    {
        return await _db.Wishlists
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.AddedAt)
            .Select(w => new WishlistItemDto(
                w.GameId,
                w.Game.Title,
                w.AddedAt,
                w.SourceShop != null ? w.SourceShop.Name : null))
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

        var existing = await _db.Wishlists.FindAsync(userId, gameId);
        if (existing != null)
        {
            _db.Wishlists.Remove(existing);
            await _db.SaveChangesAsync();
            return (false, "Removed from wishlist");
        }

        _db.Wishlists.Add(new Wishlist { UserId = userId, GameId = gameId });
        await _db.SaveChangesAsync();
        return (true, "Added to wishlist");
    }

    public async Task<(int imported, string? error)> ImportSteamAsync(int userId)
    {
        var profile = await _db.UserShopProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ShopId == 1);

        if (profile == null)
            return (0, "Steam profile not linked. Please link your Steam ID first.");

        var steamId = profile.ExternalUid;

        try
        {
            var client = _httpFactory.CreateClient();
            
            // Try multiple approaches
            // Approach 1: Try the new Steam API endpoint
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
                    request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
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

            // Check if response is HTML (error page)
            if (content.TrimStart().StartsWith("<"))
                return (0, "Steam wishlist is private or Steam ID is invalid. Please check your Steam ID and ensure your wishlist is public.");

            // Try to parse as JSON
            System.Text.Json.JsonDocument doc;
            try
            {
                doc = System.Text.Json.JsonDocument.Parse(content);
            }
            catch
            {
                return (0, "Invalid response from Steam. Please check your Steam ID.");
            }

            // Extract app IDs from response
            var appIds = new List<string>();

            // Try different JSON structures
            if (doc.RootElement.TryGetProperty("response", out var responseObj) && 
                responseObj.TryGetProperty("items", out var items))
            {
                // Steam API format
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("appid", out var appid))
                        appIds.Add(appid.GetUInt32().ToString());
                }
            }
            else
            {
                // Old wishlistdata format
                appIds = doc.RootElement.EnumerateObject()
                    .Select(p => p.Name)
                    .Where(name => uint.TryParse(name, out _))
                    .ToList();
            }

            if (appIds.Count == 0)
                return (0, "Your Steam wishlist is empty or could not be read.");

            // Match with games in our database
            var matchedGameIds = await _db.GameOffers
                .Where(o => o.ShopId == 1 && o.ExternalId != null && appIds.Contains(o.ExternalId))
                .Select(o => o.GameId)
                .Distinct()
                .ToListAsync();

            var existingGameIds = await _db.Wishlists
                .Where(w => w.UserId == userId && matchedGameIds.Contains(w.GameId))
                .Select(w => w.GameId)
                .ToListAsync();

            var toAdd = matchedGameIds.Except(existingGameIds).ToList();

            foreach (var gameId in toAdd)
                _db.Wishlists.Add(new Wishlist { UserId = userId, GameId = gameId, SourceShopId = 1 });

            await _db.SaveChangesAsync();
            return (toAdd.Count, null);
        }
        catch (Exception ex)
        {
            return (0, $"Steam import failed: {ex.Message}");
        }
    }
}
