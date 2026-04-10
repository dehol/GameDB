using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GameDB.Infrastructure.Services;

public class GameService : IGameService
{
    private readonly AppDbContext _db;
    public GameService(AppDbContext db) => _db = db;

    public async Task<(List<GameCatalogRow> items, int totalCount)> GetCatalogAsync(
        string? search, int? genreId, int? shopId, int page, int pageSize)
    {
        var query = _db.Database
            .SqlQueryRaw<GameCatalogRow>("SELECT * FROM vw_game_catalog")
            .AsNoTracking();

        var all = await query.ToListAsync();

        IEnumerable<GameCatalogRow> filtered = all;

        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(g => g.Title.Contains(search, StringComparison.OrdinalIgnoreCase));

        if (genreId.HasValue)
        {
            var gameIds = await _db.GameGenres
                .Where(gg => gg.GenreId == genreId.Value)
                .Select(gg => gg.GameId)
                .ToListAsync();
            filtered = filtered.Where(g => gameIds.Contains(g.GameId));
        }

        if (shopId.HasValue)
        {
            var gameIds = await _db.GameOffers
                .Where(o => o.ShopId == shopId.Value)
                .Select(o => o.GameId)
                .Distinct()
                .ToListAsync();
            filtered = filtered.Where(g => gameIds.Contains(g.GameId));
        }

        var list = filtered.ToList();
        var totalCount = list.Count;
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return (items, totalCount);
    }

    public async Task<Game?> GetByIdAsync(int gameId)
    {
        var cutoff = DateTime.UtcNow.AddDays(-180);

        return await _db.Games
            .Include(g => g.Developer)
            .Include(g => g.Publisher)
            .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
            .Include(g => g.Offers).ThenInclude(o => o.Shop)
            .Include(g => g.Offers).ThenInclude(o => o.PriceHistories
                .Where(ph => ph.RecordedAt >= cutoff)
                .OrderByDescending(ph => ph.RecordedAt))
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.GameId == gameId);
    }

    public async Task<List<DealScoreRow>> GetDealScoresAsync(int gameId)
    {
        var offerIds = await _db.GameOffers
            .Where(o => o.GameId == gameId)
            .Select(o => o.GameOfferId)
            .ToListAsync();

        var results = new List<DealScoreRow>();
        foreach (var id in offerIds)
        {
            var rows = await _db.Database
                .SqlQueryRaw<DealScoreRow>("SELECT * FROM fn_get_deal_score({0})", id)
                .ToListAsync();
            results.AddRange(rows);
        }
        return results;
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
