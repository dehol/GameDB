using EFCore.BulkExtensions;
using GameDB.Core.Configuration;
using GameDB.Core.Constants;
using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using GameDB.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.Services;

/// <summary>
/// Imports data from StagingGame table into main Game/GameOffer tables using bulk operations
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
            .AsNoTracking()
            .Where(s => !s.IsProcessed)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        _logger.LogInformation("📦 Importing {Count} staged games to main tables", stagedGames.Count);

        if (stagedGames.Count == 0)
        {
            _logger.LogInformation("✅ No staged games to import");
            return;
        }

        // Get existing games for matching
        var normalizedTitles = stagedGames
            .Select(s => s.NormalizedTitle)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .ToList();

        var existingGames = await _db.Games
            .AsNoTracking()
            .Where(g => g.NormalizedTitle != null && normalizedTitles.Contains(g.NormalizedTitle))
            .ToDictionaryAsync(g => g.NormalizedTitle!, ct);

        // Get existing offers
        var externalIds = stagedGames
            .SelectMany(s => new[] { s.SteamAppId, s.GogId, s.EgsId })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var existingOffers = await _db.GameOffers
            .AsNoTracking()
            .Where(o => externalIds.Contains(o.ExternalId))
            .ToDictionaryAsync(o => $"{o.ShopId}:{o.ExternalId}", ct);

        // Get existing genre links
        var existingGameIds = existingGames.Values.Select(g => g.GameId).Distinct().ToList();
        var existingGenreLinks = await _db.GameGenres
            .AsNoTracking()
            .Where(gg => existingGameIds.Contains(gg.GameId))
            .ToListAsync(ct);
        var gameGenreMap = existingGenreLinks
            .GroupBy(gg => gg.GameId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.GenreId).ToHashSet());

        // Prepare collections for bulk operations
        var newGames = new List<Game>();
        var gamesToUpdate = new List<Game>();
        var newOffers = new List<GameOffer>();
        var offersToUpdate = new List<GameOffer>();
        var newGameGenres = new List<GameGenre>();
        var processedStagingIds = new List<int>();

        foreach (var staged in stagedGames)
        {
            if (string.IsNullOrWhiteSpace(staged.NormalizedTitle))
                continue;

            try
            {
                if (existingGames.TryGetValue(staged.NormalizedTitle, out var existingGame))
                {
                    // Update existing game
                    var needsUpdate = false;
                    
                    if (!string.IsNullOrEmpty(staged.IgdbId))
                    {
                        if (staged.Description != null && staged.Description != existingGame.Description)
                        {
                            existingGame.Description = staged.Description;
                            needsUpdate = true;
                        }
                        if (staged.ReleaseDate.HasValue && staged.ReleaseDate != existingGame.ReleaseDate)
                        {
                            existingGame.ReleaseDate = staged.ReleaseDate;
                            needsUpdate = true;
                        }
                        
                        var devId = await _cache.GetOrCreateDeveloperIdAsync(staged.Developer);
                        var pubId = await _cache.GetOrCreatePublisherIdAsync(staged.Publisher);
                        
                        if (devId.HasValue && existingGame.DeveloperId != devId)
                        {
                            existingGame.DeveloperId = devId;
                            needsUpdate = true;
                        }
                        if (pubId.HasValue && existingGame.PublisherId != pubId)
                        {
                            existingGame.PublisherId = pubId;
                            needsUpdate = true;
                        }
                    }

                    if (needsUpdate)
                    {
                        existingGame.UpdatedAt = DateTime.UtcNow;
                        gamesToUpdate.Add(existingGame);
                    }

                    // Add missing genres
                    foreach (var genreName in staged.Genres.Where(g => !string.IsNullOrWhiteSpace(g)))
                    {
                        var genreId = await _cache.GetOrCreateGenreIdAsync(genreName!);
                        if (!gameGenreMap.TryGetValue(existingGame.GameId, out var genreSet))
                        {
                            genreSet = new HashSet<int>();
                            gameGenreMap[existingGame.GameId] = genreSet;
                        }
                        if (!genreSet.Contains(genreId))
                        {
                            newGameGenres.Add(new GameGenre { GameId = existingGame.GameId, GenreId = genreId });
                            genreSet.Add(genreId);
                        }
                    }

                    // Create/update offers
                    ProcessOffers(existingGame.GameId, staged, existingOffers, newOffers, offersToUpdate);
                    
                    staged.GameId = existingGame.GameId;
                }
                else
                {
                    // Create new game
                    var game = new Game
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

                    newGames.Add(game);
                    existingGames[game.NormalizedTitle] = game;

                    // Prepare genres (will be added after bulk insert when we have GameId)
                    staged.GameId = -1; // Mark as new, will be updated after bulk insert

                    // Prepare offers (will be added after bulk insert)
                }

                processedStagingIds.Add(staged.StagingGameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to prepare staged game {Id}", staged.StagingGameId);
                job.ErrorCount++;
            }
        }

        // Bulk operations
        var totalGamesCreated = 0;
        var totalOffersCreated = 0;

        if (newGames.Count > 0)
        {
            _logger.LogInformation("📦 Bulk inserting {Count} new games", newGames.Count);
            await _db.BulkInsertAsync(newGames, cancellationToken: ct);
            totalGamesCreated = newGames.Count;

            // Now add genres and offers for new games
            foreach (var staged in stagedGames.Where(s => s.GameId == -1))
            {
                var game = newGames.FirstOrDefault(g => g.NormalizedTitle == staged.NormalizedTitle);
                if (game == null) continue;

                staged.GameId = game.GameId;

                // Add genres
                foreach (var genreName in staged.Genres.Where(g => !string.IsNullOrWhiteSpace(g)))
                {
                    var genreId = await _cache.GetOrCreateGenreIdAsync(genreName!);
                    newGameGenres.Add(new GameGenre { GameId = game.GameId, GenreId = genreId });
                }

                // Create offers
                ProcessOffers(game.GameId, staged, existingOffers, newOffers, offersToUpdate);
            }
        }

        if (gamesToUpdate.Count > 0)
        {
            _logger.LogInformation("📦 Bulk updating {Count} existing games", gamesToUpdate.Count);
            await _db.BulkUpdateAsync(gamesToUpdate, cancellationToken: ct);
        }

        if (newGameGenres.Count > 0)
        {
            _logger.LogInformation("📦 Bulk inserting {Count} game genres", newGameGenres.Count);
            await _db.BulkInsertAsync(newGameGenres, cancellationToken: ct);
        }

        if (newOffers.Count > 0)
        {
            _logger.LogInformation("📦 Bulk inserting {Count} new offers", newOffers.Count);
            await _db.BulkInsertAsync(newOffers, cancellationToken: ct);
            totalOffersCreated = newOffers.Count;
        }

        if (offersToUpdate.Count > 0)
        {
            _logger.LogInformation("📦 Bulk updating {Count} existing offers", offersToUpdate.Count);
            await _db.BulkUpdateAsync(offersToUpdate, cancellationToken: ct);
        }

        // Mark staged games as processed
        if (processedStagingIds.Count > 0)
        {
            // Create a lookup for GameId by StagingGameId
            var stagingGameIdLookup = stagedGames
                .Where(s => s.GameId > 0)
                .ToDictionary(s => s.StagingGameId, s => s.GameId);

            await _db.StagingGames
                .Where(s => processedStagingIds.Contains(s.StagingGameId))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsProcessed, true),
                    ct);
            
            // Update GameId separately for games that were imported
            foreach (var kvp in stagingGameIdLookup)
            {
                var staging = await _db.StagingGames.FindAsync(kvp.Key);
                if (staging != null)
                {
                    staging.GameId = kvp.Value;
                }
            }
            await _db.SaveChangesAsync(ct);
        }

        job.TotalGamesCreated += totalGamesCreated;
        job.TotalOffersCreated += totalOffersCreated + offersToUpdate.Count;

        _logger.LogInformation(
            "✅ Import complete: {GamesCreated} new games, {GamesUpdated} updated, {OffersCreated} new offers, {OffersUpdated} updated",
            totalGamesCreated, gamesToUpdate.Count, totalOffersCreated, offersToUpdate.Count);

        _cache.Clear();
    }

    private void ProcessOffers(
        int gameId,
        StagingGame staged,
        Dictionary<string, GameOffer> existingOffers,
        List<GameOffer> newOffers,
        List<GameOffer> offersToUpdate)
    {
        if (!string.IsNullOrEmpty(staged.SteamAppId))
        {
            CreateOrUpdateOfferInternal(gameId, ShopConstants.Steam, staged.SteamAppId,
                staged.SteamPrice, staged.SteamDiscount, existingOffers, newOffers, offersToUpdate);
        }

        if (!string.IsNullOrEmpty(staged.GogId))
        {
            CreateOrUpdateOfferInternal(gameId, ShopConstants.Gog, staged.GogId,
                staged.GogPrice, staged.GogDiscount, existingOffers, newOffers, offersToUpdate);
        }

        if (!string.IsNullOrEmpty(staged.EgsId))
        {
            CreateOrUpdateOfferInternal(gameId, ShopConstants.EpicGames, staged.EgsId,
                staged.EgsPrice, staged.EgsDiscount, existingOffers, newOffers, offersToUpdate);
        }
    }

    private void CreateOrUpdateOfferInternal(
        int gameId,
        int shopId,
        string externalId,
        decimal? price,
        short? discount,
        Dictionary<string, GameOffer> existingOffers,
        List<GameOffer> newOffers,
        List<GameOffer> offersToUpdate)
    {
        var offerKey = $"{shopId}:{externalId}";
        existingOffers.TryGetValue(offerKey, out var existingOffer);

        if (existingOffer != null)
        {
            if (price.HasValue && (existingOffer.CurrentPrice != price.Value || existingOffer.CurrentDiscount != (discount ?? 0)))
            {
                existingOffer.CurrentPrice = price.Value;
                existingOffer.CurrentDiscount = discount ?? 0;
                existingOffer.PriceSyncedAt = DateTime.UtcNow;
                offersToUpdate.Add(existingOffer);
            }
        }
        else
        {
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

            newOffers.Add(offer);
            existingOffers[offerKey] = offer;
        }
    }
}
