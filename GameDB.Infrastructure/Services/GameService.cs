using GameDB.Core.DTOs;
using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using GameDB.Core.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace GameDB.Infrastructure.Services;

public class GameService
{
    private readonly AppDbContext _db;
    private readonly IIgdbApiService _igdbApiService;
    private readonly ILogger<GameService> _logger;
    private static readonly Regex SteamAppIdFromUrlRegex = new(@"/app/(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DigitsRegex = new(@"^\d+$", RegexOptions.Compiled);

    public GameService(
        AppDbContext db,
        IIgdbApiService igdbApiService,
        ILogger<GameService> logger)
    {
        _db = db;
        _igdbApiService = igdbApiService;
        _logger = logger;
    }

    public async Task<(List<GameCatalogRow> items, int totalCount)> GetCatalogAsync(
        string? search, int? genreId, int? shopId, string? sortBy, string? contentType, int page, int pageSize)
    {
        var catalogQuerySql = await BuildCatalogQuerySqlAsync();

        // Base query from view - EF Core 7+ allows composing LINQ over raw SQL
        var query = _db.Database
            .SqlQueryRaw<GameCatalogRow>(catalogQuerySql)
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

        var normalizedContentType = NormalizeContentTypeFilter(contentType);
        if (!string.IsNullOrEmpty(normalizedContentType) && normalizedContentType != "all")
        {
            query = query.Where(g => (g.content_type ?? "main_game") == normalizedContentType);
        }

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

    private async Task<string> BuildCatalogQuerySqlAsync()
    {
        var hasCatalogContentType = await HasColumnAsync("vw_game_catalog", "content_type");
        if (hasCatalogContentType)
            return "SELECT * FROM vw_game_catalog";

        _logger.LogWarning("vw_game_catalog.content_type is missing. Using compatibility projection.");

        var hasGameContentType = await HasColumnAsync("Game", "ContentType");
        if (hasGameContentType)
        {
            return """
                SELECT
                    v.*,
                    COALESCE(g."ContentType",
                        CASE WHEN COALESCE(v.is_dlc, false) THEN 'dlc_addon' ELSE 'main_game' END
                    ) AS content_type
                FROM vw_game_catalog v
                LEFT JOIN "Game" g ON g."GameId" = v."GameId"
                """;
        }

        return """
            SELECT
                v.*,
                CASE WHEN COALESCE(v.is_dlc, false) THEN 'dlc_addon' ELSE 'main_game' END AS content_type
            FROM vw_game_catalog v
            """;
    }

    private async Task<bool> HasColumnAsync(string tableName, string columnName)
    {
        return await _db.Database
            .SqlQueryRaw<int>(
                """
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = {0}
                  AND column_name = {1}
                LIMIT 1
                """,
                tableName,
                columnName)
            .AnyAsync();
    }

    private static string? NormalizeContentTypeFilter(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return null;

        return contentType.Trim().ToLowerInvariant() switch
        {
            "all" => "all",
            "main-game" or "main" or "game" or "games" => "main_game",
            "dlc" or "dlcs" => "dlc_addon",
            "bundles" => "bundle",
            _ => contentType.Trim().ToLowerInvariant()
        };
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

    /// <summary>
    /// Resolve cover sources for a set of games.
    /// Priority: 1) cached Game.CoverUrl (from IGDB import), 2) Steam CDN fallback
    /// Returns dictionary of gameId → cover source string.
    /// Cover source formats: "steam:{appId}" or full URL (https://...)
    /// </summary>
    public async Task<Dictionary<int, string>> GetCoverSourcesAsync(IEnumerable<int> gameIds)
    {
        var ids = gameIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, string>();

        var result = new Dictionary<int, string>();

        // Step 1: Return cached CoverUrl from Game table (populated during IGDB import)
        var games = await _db.Games
            .AsNoTracking()
            .Where(g => ids.Contains(g.GameId))
            .Select(g => new { g.GameId, g.CoverUrl, g.RawgId })
            .ToListAsync();

        foreach (var game in games)
        {
            // Skip stale full Steam CDN URLs from previous code version
            if (!string.IsNullOrWhiteSpace(game.CoverUrl) && !game.CoverUrl.StartsWith("https://cdn.cloudflare.steamstatic.com/") && !game.CoverUrl.StartsWith("https://shared.cloudflare.steamstatic.com/"))
            {
                result[game.GameId] = game.CoverUrl;
            }
        }

        var unresolvedIds = ids.Where(id => !result.ContainsKey(id)).ToHashSet();
        if (unresolvedIds.Count == 0) return result;

        // Step 2: Steam CDN fallback — for Steam games without an IGDB cover
        var steamOffers = await _db.GameOffers
            .AsNoTracking()
            .Where(o => unresolvedIds.Contains(o.GameId) && o.ShopId == ShopConstants.Steam)
            .Select(o => new { o.GameId, o.ExternalId, o.DownloadUrl })
            .ToListAsync();

        var resolvedBySteam = new Dictionary<int, string>();
        foreach (var offer in steamOffers)
        {
            if (result.ContainsKey(offer.GameId)) continue;

            var steamAppId = TryExtractSteamAppId(offer.ExternalId) ?? TryExtractSteamAppId(offer.DownloadUrl);
            if (!string.IsNullOrWhiteSpace(steamAppId))
            {
                // "steam:{appId}" — frontend generates multiple CDN fallback URLs
                var coverSource = $"steam:{steamAppId}";
                resolvedBySteam[offer.GameId] = coverSource;
                result[offer.GameId] = coverSource;
            }
        }

        // Cache Steam fallback to Game.CoverUrl so we skip this next time
        if (resolvedBySteam.Count > 0)
        {
            await SaveCoverUrlsAsync(resolvedBySteam);
        }

        unresolvedIds = ids.Where(id => !result.ContainsKey(id)).ToHashSet();
        if (unresolvedIds.Count == 0) return result;

        // Step 3: IGDB cover lookup by RawgId (which stores the IGDB game ID)
        // For games imported before cover.url was added to the IGDB query
        var unresolvedWithIgdbId = games
            .Where(g => unresolvedIds.Contains(g.GameId) && g.RawgId != null)
            .ToList();

        if (unresolvedWithIgdbId.Count > 0)
        {
            var igdbIds = unresolvedWithIgdbId.Select(g => g.RawgId!.Value).ToList();
            var igdbCovers = await _igdbApiService.GetCoversByIdsAsync(igdbIds);

            var resolvedByIgdb = new Dictionary<int, string>();
            foreach (var game in unresolvedWithIgdbId)
            {
                if (igdbCovers.TryGetValue(game.RawgId!.Value, out var coverUrl))
                {
                    resolvedByIgdb[game.GameId] = coverUrl;
                    result[game.GameId] = coverUrl;
                }
            }

            if (resolvedByIgdb.Count > 0)
            {
                await SaveCoverUrlsAsync(resolvedByIgdb);
            }
        }

        return result;
    }

    private async Task SaveCoverUrlsAsync(Dictionary<int, string> coverUrls)
    {
        if (coverUrls.Count == 0) return;

        var gameIds = coverUrls.Keys.ToList();
        var gamesToUpdate = await _db.Games
            .Where(g => gameIds.Contains(g.GameId) && g.CoverUrl == null)
            .ToListAsync();

        foreach (var game in gamesToUpdate)
        {
            if (coverUrls.TryGetValue(game.GameId, out var url))
            {
                game.CoverUrl = url;
            }
        }

        if (gamesToUpdate.Count > 0)
        {
            await _db.SaveChangesAsync();
            _logger.LogDebug("Cached {Count} cover URLs", gamesToUpdate.Count);
        }
    }

    private static string? TryExtractSteamAppId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var fromUrl = SteamAppIdFromUrlRegex.Match(value);
        if (fromUrl.Success) return fromUrl.Groups[1].Value;

        var trimmed = value.Trim();
        return DigitsRegex.IsMatch(trimmed) ? trimmed : null;
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
