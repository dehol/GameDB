using GameDB.Core.Configuration;
using GameDB.Core.Constants;
using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using GameDB.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.Services;

/// <summary>
/// Imports data from StagingGame table into main Game/GameOffer tables
/// </summary>
public class DataImportService : IDataImportService
{
    private readonly AppDbContext _db;
    private readonly ReferenceDataCache _cache;
    private readonly ILogger<DataImportService> _logger;
    private readonly ImportSettings _settings;

    public DataImportService(
        AppDbContext db,
        ReferenceDataCache cache,
        ILogger<DataImportService> logger,
        ImportSettings settings)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
        _settings = settings;
    }

    public async Task ImportStagedDataAsync(ImportJob job, CancellationToken ct)
    {
        job.CurrentPhase = "importing";
        await _db.SaveChangesAsync(ct);

        await _cache.PreloadAsync();

        var stagedGames = await _db.StagingGames
            .Where(s => !s.IsProcessed)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        _logger.LogInformation("📦 Importing {Count} staged games to main tables", stagedGames.Count);

        var totalBatches = (int)Math.Ceiling((double)stagedGames.Count / _settings.BatchSize);
        var currentBatch = 0;

        foreach (var batch in stagedGames.Chunk(_settings.BatchSize))
        {
            currentBatch++;
            var batchList = batch.ToList();
            var batchGamesCreated = 0;
            var batchOffersCreated = 0;

            var normalizedTitles = batchList
                .Select(s => s.NormalizedTitle)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .Distinct()
                .ToList();

            var existingGames = await _db.Games
                .Where(g => g.NormalizedTitle != null && normalizedTitles.Contains(g.NormalizedTitle))
                .ToDictionaryAsync(g => g.NormalizedTitle!, ct);

            var externalIds = batchList
                .SelectMany(s => new[] { s.SteamAppId, s.GogId, s.EgsId })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            var existingOffers = await _db.GameOffers
                .Where(o =>
                    (o.ShopId == ShopConstants.Steam ||
                     o.ShopId == ShopConstants.Gog ||
                     o.ShopId == ShopConstants.EpicGames) &&
                    externalIds.Contains(o.ExternalId))
                .ToDictionaryAsync(o => $"{o.ShopId}:{o.ExternalId}", ct);

            var existingGameIds = existingGames.Values
                .Select(g => g.GameId)
                .Distinct()
                .ToList();
            var existingGenreLinks = await _db.GameGenres
                .Where(gg => existingGameIds.Contains(gg.GameId))
                .ToListAsync(ct);
            var gameGenreMap = existingGenreLinks
                .GroupBy(gg => gg.GameId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.GenreId).ToHashSet());

            foreach (var staged in batchList)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(staged.NormalizedTitle))
                    {
                        continue;
                    }

                    var (gameCreated, offersCreated) = await ImportSingleGameAsync(
                        staged, existingGames, existingOffers, gameGenreMap, ct);
                    if (gameCreated) batchGamesCreated++;
                    batchOffersCreated += offersCreated;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to import staged game {Id}", staged.StagingGameId);
                    job.ErrorCount++;
                }
            }

            job.TotalGamesCreated += batchGamesCreated;
            job.TotalOffersCreated += batchOffersCreated;
            await _db.SaveChangesAsync(ct);

            if (_settings.EnableDetailedLogging)
            {
                _logger.LogInformation(
                    "📦 Batch {Current}/{Total}: {GamesCreated} games, {OffersCreated} offers",
                    currentBatch, totalBatches, batchGamesCreated, batchOffersCreated);
            }
        }

        _logger.LogInformation(
            "✅ Import complete: {GamesCreated} games, {OffersCreated} offers",
            job.TotalGamesCreated, job.TotalOffersCreated);

        _cache.Clear();
    }

    private async Task<(bool gameCreated, int offersCreated)> ImportSingleGameAsync(
        StagingGame staged,
        Dictionary<string, Game> existingGames,
        Dictionary<string, GameOffer> existingOffers,
        Dictionary<int, HashSet<int>> gameGenreMap,
        CancellationToken ct)
    {
        var gameCreated = false;
        var offersCreated = 0;
        existingGames.TryGetValue(staged.NormalizedTitle, out var existingGame);

        Game game;
        if (existingGame != null)
        {
            game = existingGame;

            if (!string.IsNullOrEmpty(staged.IgdbId))
            {
                game.Description = staged.Description ?? game.Description;
                game.ReleaseDate = staged.ReleaseDate ?? game.ReleaseDate;
                game.DeveloperId ??= await _cache.GetOrCreateDeveloperIdAsync(staged.Developer);
                game.PublisherId ??= await _cache.GetOrCreatePublisherIdAsync(staged.Publisher);
                game.UpdatedAt = DateTime.UtcNow;
            }

            await AddMissingGenresAsync(game.GameId, staged.Genres, gameGenreMap);
        }
        else
        {
            game = new Game
            {
                Title = staged.Title,
                NormalizedTitle = staged.NormalizedTitle,
                Description = staged.Description,
                ReleaseDate = staged.ReleaseDate,
                DeveloperId = await _cache.GetOrCreateDeveloperIdAsync(staged.Developer),
                PublisherId = await _cache.GetOrCreatePublisherIdAsync(staged.Publisher),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Games.Add(game);
            await _db.SaveChangesAsync(ct);
            existingGames[game.NormalizedTitle] = game;

            foreach (var genreName in staged.Genres)
            {
                var genreId = await _cache.GetOrCreateGenreIdAsync(genreName);
                _db.GameGenres.Add(new GameGenre
                {
                    GameId = game.GameId,
                    GenreId = genreId
                });

                if (!gameGenreMap.TryGetValue(game.GameId, out var genreSet))
                {
                    genreSet = new HashSet<int>();
                    gameGenreMap[game.GameId] = genreSet;
                }
                genreSet.Add(genreId);
            }

            gameCreated = true;
        }

        staged.GameId = game.GameId;
        staged.IsProcessed = true;

        offersCreated = await CreateOffersAsync(game.GameId, staged, existingOffers);

        return (gameCreated, offersCreated);
    }

    private async Task AddMissingGenresAsync(
        int gameId,
        IEnumerable<string> genreNames,
        Dictionary<int, HashSet<int>> gameGenreMap)
    {
        if (!gameGenreMap.TryGetValue(gameId, out var existingGenreIds))
        {
            existingGenreIds = new HashSet<int>();
            gameGenreMap[gameId] = existingGenreIds;
        }

        foreach (var genreName in genreNames)
        {
            if (string.IsNullOrWhiteSpace(genreName))
            {
                continue;
            }

            var genreId = await _cache.GetOrCreateGenreIdAsync(genreName);
            if (existingGenreIds.Contains(genreId))
            {
                continue;
            }

            _db.GameGenres.Add(new GameGenre
            {
                GameId = gameId,
                GenreId = genreId
            });
            existingGenreIds.Add(genreId);
        }
    }
    private Task<int> CreateOffersAsync(
        int gameId,
        StagingGame staged,
        Dictionary<string, GameOffer> existingOffers)
    {
        var offersCreated = 0;

        if (!string.IsNullOrEmpty(staged.SteamAppId))
        {
            if (CreateOrUpdateOffer(gameId, ShopConstants.Steam, staged.SteamAppId,
                staged.SteamPrice, staged.SteamDiscount, existingOffers))
                offersCreated++;
        }

        if (!string.IsNullOrEmpty(staged.GogId))
        {
            if (CreateOrUpdateOffer(gameId, ShopConstants.Gog, staged.GogId,
                staged.GogPrice, staged.GogDiscount, existingOffers))
                offersCreated++;
        }

        if (!string.IsNullOrEmpty(staged.EgsId))
        {
            if (CreateOrUpdateOffer(gameId, ShopConstants.EpicGames, staged.EgsId,
                staged.EgsPrice, staged.EgsDiscount, existingOffers))
                offersCreated++;
        }

        return Task.FromResult(offersCreated);
    }

    private bool CreateOrUpdateOffer(
        int gameId,
        int shopId,
        string externalId,
        decimal? price,
        short? discount,
        Dictionary<string, GameOffer> existingOffers)
    {
        var offerKey = $"{shopId}:{externalId}";
        existingOffers.TryGetValue(offerKey, out var existingOffer);

        if (existingOffer != null)
        {
            if (price.HasValue)
            {
                existingOffer.CurrentPrice = price.Value;
                existingOffer.CurrentDiscount = discount ?? 0;
                existingOffer.PriceSyncedAt = DateTime.UtcNow;
            }
            return false;
        }

        var offer = new GameOffer
        {
            GameId = gameId,
            ShopId = shopId,
            ExternalId = externalId,
            CurrentPrice = price ?? 0,
            CurrentDiscount = discount ?? 0,
            Currency = "USD",
            DownloadUrl = ShopConstants.GetStoreUrl(shopId, externalId),
            PriceSyncedAt = DateTime.UtcNow
        };

        _db.GameOffers.Add(offer);
        existingOffers[offerKey] = offer;
        return true;
    }
}
