using GameDB.Core.DTOs;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GameDB.Infrastructure.Services;

public class GameService
{
    private readonly AppDbContext _db;
    public GameService(AppDbContext db) => _db = db;

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
            "games" => query.Where(g =>
                !g.is_dlc &&
                !EF.Functions.ILike(g.Title, "% dlc%") &&
                !EF.Functions.ILike(g.Title, "%map pack%") &&
                !EF.Functions.ILike(g.Title, "%season pass%") &&
                !EF.Functions.ILike(g.Title, "%soundtrack%") &&
                !EF.Functions.ILike(g.Title, "% add-on%") &&
                !EF.Functions.ILike(g.Title, "% addon%")),
            "dlc" => query.Where(g =>
                g.is_dlc ||
                EF.Functions.ILike(g.Title, "% dlc%") ||
                EF.Functions.ILike(g.Title, "%map pack%") ||
                EF.Functions.ILike(g.Title, "%season pass%") ||
                EF.Functions.ILike(g.Title, "%soundtrack%") ||
                EF.Functions.ILike(g.Title, "% add-on%") ||
                EF.Functions.ILike(g.Title, "% addon%")),
            _ => query
        };

        // Count before pagination
        var totalCount = await query.CountAsync();

        var sortKey = sortBy?.ToLowerInvariant();

        // Weighted rating baseline for "Popularity" sorting
        const int bayesianPriorVotes = 100;
        var globalAverageRating = 0d;
        if (sortKey is "rating" or "popularity")
        {
            globalAverageRating = await query
                .Where(g => g.rating > 0 && g.rating_count > 0)
                .AverageAsync(g => (double?)g.rating) ?? 0d;
        }

        // Apply sorting
        query = sortKey switch
        {
            "rating" => query
                .OrderByDescending(g =>
                    (g.rating ?? 0) > 0 && (g.rating_count ?? 0) > 0
                        ? ((((g.rating_count ?? 0) * (g.rating ?? 0d)) + (bayesianPriorVotes * globalAverageRating))
                            / ((g.rating_count ?? 0) + bayesianPriorVotes))
                        : 0d)
                .ThenByDescending(g => g.rating_count ?? 0)
                .ThenByDescending(g => g.rating ?? 0),
            "popularity" => query
                .OrderByDescending(g =>
                    (g.rating ?? 0) > 0 && (g.rating_count ?? 0) > 0
                        ? ((((g.rating_count ?? 0) * (g.rating ?? 0d)) + (bayesianPriorVotes * globalAverageRating))
                            / ((g.rating_count ?? 0) + bayesianPriorVotes))
                        : 0d)
                .ThenByDescending(g => g.rating_count ?? 0)
                .ThenByDescending(g => g.rating ?? 0),
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
            CoverUrl = game.CoverUrl,
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
