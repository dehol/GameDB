using GameDB.Core.DTOs;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace GameDB.Infrastructure.Services;

public class GameService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GameService> _logger;
    private static readonly Regex SteamAppIdFromUrlRegex = new(@"/app/(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DigitsRegex = new(@"^\d+$", RegexOptions.Compiled);
    private static readonly Regex OgImageRegex = new(
        "<meta[^>]+(?:property|name)=[\"'](?:og:image|twitter:image)[\"'][^>]*content=[\"'](?<url>[^\"']+)[\"'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OgImageRegexReversed = new(
        "<meta[^>]+content=[\"'](?<url>[^\"']+)[\"'][^>]*(?:property|name)=[\"'](?:og:image|twitter:image)[\"'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public GameService(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<GameService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<(List<GameCatalogRow> items, int totalCount)> GetCatalogAsync(
        string? search, int? genreId, int? shopId, string? sortBy, string? contentType, int page, int pageSize)
    {
        // Base query from view - EF Core 7+ allows composing LINQ over raw SQL
        var query = _db.Database
            .SqlQueryRaw<GameCatalogRow>("SELECT * FROM vw_game_catalog")
            .AsNoTracking();

        // Database-side filtering
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(g => g.Title.Contains(search));

        if (genreId.HasValue)
        {
            var gameIds = _db.GameGenres
                .Where(gg => gg.GenreId == genreId.Value)
                .Select(gg => gg.GameId);
            query = query.Where(g => gameIds.Contains(g.GameId));
        }

        if (shopId.HasValue)
        {
            var gameIds = _db.GameOffers
                .Where(o => o.ShopId == shopId.Value)
                .Select(o => o.GameId)
                .Distinct();
            query = query.Where(g => gameIds.Contains(g.GameId));
        }

        query = contentType?.ToLowerInvariant() switch
        {
            "main_game" or "main-game" or "main" or "game" or "games" => query.Where(g =>
                !g.is_dlc &&
                !EF.Functions.ILike(g.Title, "%bundle%") &&
                !EF.Functions.ILike(g.Title, "%collection%") &&
                !EF.Functions.ILike(g.Title, "% pack%") &&
                !EF.Functions.ILike(g.Title, "%pack %") &&
                !EF.Functions.ILike(g.Title, "% pack") &&
                !EF.Functions.ILike(g.Title, "pack %") &&
                !EF.Functions.ILike(g.Title, "pack") &&
                !EF.Functions.ILike(g.Title, "% dlc%") &&
                !EF.Functions.ILike(g.Title, "%map pack%") &&
                !EF.Functions.ILike(g.Title, "%season pass%") &&
                !EF.Functions.ILike(g.Title, "%soundtrack%") &&
                !EF.Functions.ILike(g.Title, "% add-on%") &&
                !EF.Functions.ILike(g.Title, "% addon%")),
            "bundle" or "bundles" => query.Where(g =>
                EF.Functions.ILike(g.Title, "%bundle%") ||
                EF.Functions.ILike(g.Title, "%collection%")),
            "dlc" or "dlcs" => query.Where(g =>
                g.is_dlc ||
                EF.Functions.ILike(g.Title, "% dlc%") ||
                EF.Functions.ILike(g.Title, "% pack%") ||
                EF.Functions.ILike(g.Title, "%pack %") ||
                EF.Functions.ILike(g.Title, "% pack") ||
                EF.Functions.ILike(g.Title, "pack %") ||
                EF.Functions.ILike(g.Title, "pack") ||
                EF.Functions.ILike(g.Title, "%map pack%") ||
                EF.Functions.ILike(g.Title, "%season pass%") ||
                EF.Functions.ILike(g.Title, "%soundtrack%") ||
                EF.Functions.ILike(g.Title, "% add-on%") ||
                EF.Functions.ILike(g.Title, "% addon%")),
            _ => query
        };

        // Count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = sortBy?.ToLower() switch
        {
            "rating" => query
                .Where(g => g.rating > 0)  // Only games with rating
                .OrderByDescending(g => g.rating),
            "price_asc" => query.OrderBy(g => g.min_price ?? decimal.MaxValue),
            "price_desc" => query.OrderByDescending(g => g.min_price ?? 0),
            "discount" => query.OrderByDescending(g => g.max_discount ?? 0),
            "name" => query.OrderBy(g => g.Title),
            "newest" => query.OrderByDescending(g => g.ReleaseDate ?? DateOnly.MinValue),
            _ => query.OrderByDescending(g => g.UpdatedAt) // relevance / default
        };

        // Apply pagination at database level
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<GameDetailsDto?> GetByIdAsync(int gameId)
    {
        var cutoff = DateTime.UtcNow.AddDays(-180);

        var game = await _db.Games
            .Include(g => g.Developer)
            .Include(g => g.Publisher)
            .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
            .Include(g => g.Offers).ThenInclude(o => o.Shop)
            .Include(g => g.Offers).ThenInclude(o => o.PriceHistories
                .Where(ph => ph.RecordedAt >= cutoff)
                .OrderByDescending(ph => ph.RecordedAt))
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.GameId == gameId);

        if (game == null) return null;

        return new GameDetailsDto
        {
            GameId = game.GameId,
            Title = game.Title,
            Description = game.Description,
            ReleaseDate = game.ReleaseDate,
            CreatedAt = game.CreatedAt,
            UpdatedAt = game.UpdatedAt,
            Rating = game.Rating,
            RatingCount = game.RatingCount,
            Developer = game.Developer == null ? null : new DeveloperDto 
            { 
                DeveloperId = game.Developer.DeveloperId, 
                Name = game.Developer.Name 
            },
            Publisher = game.Publisher == null ? null : new PublisherDto 
            { 
                PublisherId = game.Publisher.PublisherId, 
                Name = game.Publisher.Name 
            },
            Genres = game.GameGenres.Select(gg => gg.Genre.Name).ToList(),
            Offers = game.Offers.Select(o => new GameOfferDto
            {
                GameOfferId = o.GameOfferId,
                ShopId = o.ShopId,
                ShopName = o.Shop.Name,
                ExternalId = o.ExternalId,
                DownloadUrl = o.DownloadUrl,
                CurrentPrice = o.CurrentPrice,
                CurrentDiscount = o.CurrentDiscount,
                Currency = o.Currency,
                PriceSyncedAt = o.PriceSyncedAt,
                PriceHistory = o.PriceHistories.Select(ph => new PriceHistoryDto
                {
                    PriceHistoryId = ph.PriceHistoryId,
                    Price = ph.Price,
                    DiscountPercent = ph.DiscountPercent,
                    RecordedAt = ph.RecordedAt
                }).ToList()
            }).ToList()
        };
    }

    public async Task<Dictionary<int, string>> GetCoverSourcesAsync(IEnumerable<int> gameIds)
    {
        var ids = gameIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, string>();

        var offers = await _db.GameOffers
            .AsNoTracking()
            .Where(o => ids.Contains(o.GameId))
            .Select(o => new { o.GameId, o.ExternalId, o.DownloadUrl })
            .ToListAsync();

        var result = new Dictionary<int, string>();
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(6);

        foreach (var group in offers.GroupBy(o => o.GameId))
        {
            var steamAppId = group
                .Select(o => TryExtractSteamAppId(o.ExternalId) ?? TryExtractSteamAppId(o.DownloadUrl))
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

            if (!string.IsNullOrWhiteSpace(steamAppId))
            {
                result[group.Key] = steamAppId;
                continue;
            }

            var storeUrls = group
                .Select(o => o.DownloadUrl)
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct()
                .ToList();

            var imageUrl = await TryResolveStoreImageUrlAsync(client, storeUrls);
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                result[group.Key] = imageUrl;
            }
        }

        return result;
    }

    private static string? TryExtractSteamAppId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var fromUrl = SteamAppIdFromUrlRegex.Match(value);
        if (fromUrl.Success) return fromUrl.Groups[1].Value;

        var trimmed = value.Trim();
        return DigitsRegex.IsMatch(trimmed) ? trimmed : null;
    }

    private async Task<string?> TryResolveStoreImageUrlAsync(HttpClient client, IEnumerable<string?> storeUrls)
    {
        foreach (var rawUrl in storeUrls)
        {
            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var pageUri)) continue;
            if (pageUri.Scheme is not ("http" or "https")) continue;

            try
            {
                using var response = await client.GetAsync(pageUri, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode) continue;

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (!string.Equals(contentType, "text/html", StringComparison.OrdinalIgnoreCase)) continue;

                var html = await response.Content.ReadAsStringAsync();
                var imageUrl = TryExtractMetaImageUrl(html, pageUri);
                if (!string.IsNullOrWhiteSpace(imageUrl)) return imageUrl;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not resolve cover image from {StoreUrl}", rawUrl);
            }
        }

        return null;
    }

    private static string? TryExtractMetaImageUrl(string html, Uri pageUri)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var match = OgImageRegex.Match(html);
        if (!match.Success) match = OgImageRegexReversed.Match(html);
        if (!match.Success) return null;

        var rawUrl = match.Groups["url"].Value;
        if (string.IsNullOrWhiteSpace(rawUrl)) return null;

        if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var absolute)) return absolute.ToString();
        if (Uri.TryCreate(pageUri, rawUrl, out var relative)) return relative.ToString();
        return null;
    }

    /// <summary>
    /// Get deal scores for all offers of a game using single DB function call
    /// Fixes N+1 query problem
    /// </summary>
    public async Task<List<DealScoreRow>> GetDealScoresAsync(int gameId)
    {
        // Single query - uses fn_get_game_deal_scores function
        return await _db.Database
            .SqlQueryRaw<DealScoreRow>(
                "SELECT * FROM fn_get_game_deal_scores({0})", 
                gameId)
            .ToListAsync();
    }

    public async Task<Game> CreateAsync(Game game, List<int> genreIds)
    {
        _db.Games.Add(game);
        await _db.SaveChangesAsync();

        foreach (var gid in genreIds)
            _db.GameGenres.Add(new GameGenre { GameId = game.GameId, GenreId = gid });

        await _db.SaveChangesAsync();
        return game;
    }

    public async Task<Game?> UpdateAsync(int gameId, Game updated, List<int> genreIds)
    {
        var game = await _db.Games
            .Include(g => g.GameGenres)
            .FirstOrDefaultAsync(g => g.GameId == gameId);

        if (game == null) return null;

        game.Title = updated.Title;
        game.Description = updated.Description;
        game.ReleaseDate = updated.ReleaseDate;
        game.DeveloperId = updated.DeveloperId;
        game.PublisherId = updated.PublisherId;

        _db.GameGenres.RemoveRange(game.GameGenres);
        foreach (var gid in genreIds)
            _db.GameGenres.Add(new GameGenre { GameId = gameId, GenreId = gid });

        await _db.SaveChangesAsync();
        return game;
    }

    public async Task<bool> DeleteAsync(int gameId)
    {
        var game = await _db.Games.FindAsync(gameId);
        if (game == null) return false;
        _db.Games.Remove(game);
        await _db.SaveChangesAsync();
        return true;
    }
}
