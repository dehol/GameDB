using EFCore.BulkExtensions;
using GameDB.Core.Configuration;
using GameDB.Core.Constants;
using GameDB.Core.DTOs;
using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using GameDB.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameDB.Infrastructure.Services;

public class GameImportService
{
    private readonly AppDbContext _db;
    private readonly IIgdbApiService _igdb;
    private readonly ReferenceDataCache _cache;
    private readonly ILogger<GameImportService> _logger;
    private readonly ImportSettings _settings;
    private readonly ResiliencePipeline _igdbPipeline;

    public GameImportService(
        AppDbContext db,
        IIgdbApiService igdb,
        ReferenceDataCache cache,
        ILogger<GameImportService> logger,
        ImportSettings settings)
    {
        _db = db;
        _igdb = igdb;
        _cache = cache;
        _logger = logger;
        _settings = settings;
        _igdbPipeline = BuildExternalApiPipeline();
    }

    public async Task RunImportAsync(ImportJob job, CancellationToken ct)
    {
        await RunImportAsync(job, new ImportPipelineOptions(), Array.Empty<IDataProvider>(), ct);
    }

    public async Task RunImportAsync(ImportJob job, ImportPipelineOptions options, CancellationToken ct)
    {
        await RunImportAsync(job, options, Array.Empty<IDataProvider>(), ct);
    }

    public async Task RunImportAsync(
        ImportJob job,
        ImportPipelineOptions options,
        IEnumerable<IDataProvider> dataProviders,
        CancellationToken ct)
    {
        job.Status = ImportJobStatus.Running;
        job.CurrentPhase = "collecting";
        await _db.SaveChangesAsync(ct);
        await LogJobEventAsync(job, ImportJobLogLevel.Info, "collecting", "Import pipeline started", options, ct);

        var games = await CollectFromIgdbAsync(job, options, ct);
        job.SteamTotal = games.Count;

        if (ct.IsCancellationRequested) return;

        job.CurrentPhase = "enriching_prices";
        await _db.SaveChangesAsync(ct);
        await LogJobEventAsync(job, ImportJobLogLevel.Info, "enriching_prices", "Starting source providers", null, ct);

        // Skip Steam price enrichment - prices will be synced separately via PriceSyncWorker
        _logger.LogInformation("Skipping Steam price enrichment - will be synced separately");
        var enriched = games;

        if (ct.IsCancellationRequested) return;

        job.CurrentPhase = "importing";
        await _db.SaveChangesAsync(ct);
        await LogJobEventAsync(job, ImportJobLogLevel.Info, "importing", $"Importing {enriched.Count} games", null, ct);

        await ImportToDatabaseAsync(games, job, options, ct);

        if (ct.IsCancellationRequested) return;
        job.CurrentPhase = "provider_sync";
        await _db.SaveChangesAsync(ct);

        foreach (var provider in dataProviders)
        {
            try
            {
                var updated = await provider.UpdateOffersAsync(job, ct);
                job.TotalOffersUpdated += updated;
            }
            catch (Exception ex)
            {
                job.ErrorCount++;
                await LogJobEventAsync(
                    job,
                    ImportJobLogLevel.Error,
                    "provider_sync",
                    $"Provider '{provider.Name}' failed: {ex.Message}",
                    new { provider.Name, Exception = ex.GetType().Name },
                    ct);
            }
        }

        job.Status = ImportJobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        job.CurrentPhase = "completed";
        await _db.SaveChangesAsync(ct);
        await LogJobEventAsync(
            job,
            ImportJobLogLevel.Info,
            "completed",
            "Import completed successfully",
            new
            {
                job.TotalGamesCreated,
                job.TotalOffersCreated,
                job.TotalOffersUpdated,
                job.ErrorCount
            },
            ct);

        _logger.LogInformation(
            "Import completed: {Games} games, {Offers} offers, {Errors} errors",
            job.TotalGamesCreated, job.TotalOffersCreated, job.ErrorCount);
    }

    // ── Phase 1: Collect from IGDB ────────────────────────────────────────

    private async Task<List<GameImport>> CollectFromIgdbAsync(ImportJob job, ImportPipelineOptions options, CancellationToken ct)
    {
        // Skip already imported IGDB IDs
        var existingIgdbIds = (await _db.Games
            .AsNoTracking()
            .Where(g => g.IgdbId != null) // IGDB ID for deduplication
            .Select(g => g.IgdbId!.Value)
            .ToListAsync(ct))
            .ToHashSet();

        var includeIgdbIds = options.IgdbGameIds is { Count: > 0 }
            ? options.IgdbGameIds.ToHashSet()
            : null;
        var excludeIgdbIds = (options.OverwriteExisting || includeIgdbIds is not null)
            ? null
            : existingIgdbIds;

        var igdbGames = await _igdbPipeline.ExecuteAsync(async token =>
            await _igdb.GetPcGamesAsync(
                excludeIgdbIds: excludeIgdbIds,
                includeIgdbIds: includeIgdbIds,
                maxGames: options.Limit,
                ct: token), ct);

        var imports = igdbGames.Select(g => new GameImport
        {
            Title           = g.Name,
            NormalizedTitle = NormalizeTitle(g.Name),
            Description     = g.Summary,
            ReleaseDate     = g.FirstReleaseDate.HasValue
                ? DateOnly.FromDateTime(DateTimeOffset
                    .FromUnixTimeSeconds(g.FirstReleaseDate.Value).DateTime)
                : null,
            Developer  = g.Developer,
            Publisher  = g.Publisher,
            Genres     = g.Genres,
            IgdbId     = g.Id,
            Offers     = BuildOffers(g),
            CoverUrl   = g.CoverUrl,
            Rating     = g.Rating,
            RatingCount = g.RatingCount,
            IsDlc = g.IsDlc,
            ContentType = g.GameType
        }).Where(g => !string.IsNullOrEmpty(g.NormalizedTitle)).ToList();

        // Filter out games with no store offers
        var gamesWithOffers = imports.Where(i => i.Offers.Count > 0).ToList();
        var skippedCount = imports.Count - gamesWithOffers.Count;
        if (skippedCount > 0)
        {
            _logger.LogInformation("IGDB: Skipped {Count} games with no store offers (Steam/GOG)", skippedCount);
            await LogJobEventAsync(
                job,
                ImportJobLogLevel.Warning,
                "collecting",
                $"Skipped {skippedCount} games without offers",
                new { skippedCount },
                ct);
        }

        _logger.LogInformation("IGDB: {Count} games collected for import", gamesWithOffers.Count);
        var gamesWithGenres = gamesWithOffers.Where(i => i.Genres.Count > 0).ToList();
        _logger.LogInformation("IGDB: {Count} games have genres", gamesWithGenres.Count);
        foreach (var import in gamesWithGenres.Take(5)) // Log first 5
        {
            _logger.LogInformation("Game '{Title}' genres: {Genres}", import.Title, string.Join(", ", import.Genres));
        }
        return gamesWithOffers;
    }

    private static List<GameOfferImport> BuildOffers(IgdbGame g)
    {
        var offers = new List<GameOfferImport>();

        if (g.SteamUrl != null)
        {
            var steamId = ExtractSteamAppId(g.SteamUrl);
            if (steamId != null)
                offers.Add(new GameOfferImport
                {
                    ShopId     = ShopConstants.Steam,
                    ExternalId = steamId
                });
        }

        if (g.GogUrl != null)
        {
            var gogId = ExtractGogId(g.GogUrl);
            if (gogId != null)
                offers.Add(new GameOfferImport
                {
                    ShopId     = ShopConstants.Gog,
                    ExternalId = gogId
                });
        }

        return offers;
    }

    private static string? ExtractSteamAppId(string url)
    {
        var m = Regex.Match(url, @"/app/(\d+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? ExtractGogId(string url)
    {
        // GOG URLs: https://www.gog.com/game/game_slug
        var m = Regex.Match(url, @"gog\.com/(?:game|en/game)/([^/?#]+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    // ── Phase 2: Import to DB ─────────────────────────────────────────────

    private async Task ImportToDatabaseAsync(
        List<GameImport> games, ImportJob job, ImportPipelineOptions options, CancellationToken ct)
    {
        _logger.LogInformation("Importing {Count} games to database", games.Count);
        await _cache.PreloadAsync();

        var normalizedTitles = games
            .Select(g => g.NormalizedTitle)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct().ToList();

        var existingGames = (await _db.Games.AsNoTracking()
            .Where(g => g.NormalizedTitle != null && normalizedTitles.Contains(g.NormalizedTitle))
            .ToListAsync(ct))
            .Where(g => g.NormalizedTitle != null)
            .ToDictionary(g => g.NormalizedTitle!);

        // Load existing GameGenres for games we're about to process
        var existingGameIds = existingGames.Values.Select(g => g.GameId).ToList();
        var existingGameGenres = await _db.GameGenres
            .AsNoTracking()
            .Where(gg => existingGameIds.Contains(gg.GameId))
            .Select(gg => new { gg.GameId, gg.GenreId })
            .ToListAsync(ct);
        var existingGameGenreSet = existingGameGenres
            .Select(gg => $"{gg.GameId}:{gg.GenreId}")
            .ToHashSet();

        _logger.LogInformation("Found {ExistingGames} existing games with {ExistingGenres} genre links", 
            existingGames.Count, existingGameGenreSet.Count);

        var externalIds = games
            .SelectMany(g => g.Offers.Select(o => o.ExternalId))
            .Distinct().ToList();

        var existingOffers = await _db.GameOffers.AsNoTracking()
            .Where(o => externalIds.Contains(o.ExternalId))
            .ToDictionaryAsync(o => $"{o.ShopId}:{o.ExternalId}", ct);

        var newGames       = new List<Game>();
        var gamesToUpdate  = new List<Game>();
        var newOffers      = new List<GameOffer>();
        var offersToUpdate = new List<GameOffer>();
        var newGameGenres  = new List<GameGenre>();

        foreach (var import in games)
        {
            try
            {
                if (string.IsNullOrEmpty(import.NormalizedTitle)) continue;

                if (existingGames.TryGetValue(import.NormalizedTitle, out var existing))
                {
                    if (!options.OverwriteExisting)
                    {
                        var needsTypeRefresh = false;

                        if (existing.IsDlc != import.IsDlc)
                        {
                            existing.IsDlc = import.IsDlc;
                            needsTypeRefresh = true;
                        }

                        if (!string.Equals(existing.ContentType, import.ContentType, StringComparison.Ordinal))
                        {
                            existing.ContentType = import.ContentType;
                            needsTypeRefresh = true;
                        }

                        if (needsTypeRefresh)
                        {
                            existing.UpdatedAt = DateTime.UtcNow;
                            gamesToUpdate.Add(existing);
                            _logger.LogInformation(
                                "Refreshed content type for existing game '{Title}' (GameId={GameId}) to '{ContentType}'",
                                import.Title, existing.GameId, existing.ContentType ?? "main_game");
                        }
                        else
                        {
                            _logger.LogDebug("Skipping existing game '{Title}' - OverwriteExisting is false", import.Title);
                        }

                        continue;
                    }

                    _logger.LogInformation("Updating existing game '{Title}' (GameId={GameId})", import.Title, existing.GameId);
                    
                    var needsUpdate = false;

                    if (!string.IsNullOrEmpty(import.Description)
                        && import.Description != existing.Description)
                    { existing.Description = import.Description; needsUpdate = true; }

                    if (import.ReleaseDate.HasValue
                        && import.ReleaseDate != existing.ReleaseDate)
                    { existing.ReleaseDate = import.ReleaseDate; needsUpdate = true; }

                    var devId = await _cache.GetOrCreateDeveloperIdAsync(import.Developer);
                    var pubId = await _cache.GetOrCreatePublisherIdAsync(import.Publisher);

                    if (devId.HasValue && existing.DeveloperId != devId)
                    { existing.DeveloperId = devId; needsUpdate = true; }
                    if (pubId.HasValue && existing.PublisherId != pubId)
                    { existing.PublisherId = pubId; needsUpdate = true; }

                    // Update rating if new data available
                    if (import.Rating.HasValue && import.RatingCount.HasValue)
                    {
                        if (existing.Rating != import.Rating || existing.RatingCount != import.RatingCount)
                        {
                            existing.Rating = import.Rating;
                            existing.RatingCount = import.RatingCount;
                            needsUpdate = true;
                            _logger.LogDebug("Updated rating for '{Title}': {Rating} ({Count} votes)", 
                                import.Title, import.Rating, import.RatingCount);
                        }
                    }

                    if (existing.IsDlc != import.IsDlc)
                    {
                        existing.IsDlc = import.IsDlc;
                        needsUpdate = true;
                    }

                    if (!string.Equals(existing.ContentType, import.ContentType, StringComparison.Ordinal))
                    {
                        existing.ContentType = import.ContentType;
                        needsUpdate = true;
                    }

                    if (!string.IsNullOrEmpty(import.CoverUrl) && existing.CoverUrl != import.CoverUrl)
                    {
                        existing.CoverUrl = import.CoverUrl;
                        needsUpdate = true;
                    }

                    if (needsUpdate)
                    { existing.UpdatedAt = DateTime.UtcNow; gamesToUpdate.Add(existing); }

                    ProcessOffers(existing.GameId, import.Offers,
                        existingOffers, newOffers, offersToUpdate);
                }
                else
                {
                    var game = new Game
                    {
                        Title           = import.Title,
                        NormalizedTitle = import.NormalizedTitle,
                        Description     = import.Description,
                        ReleaseDate     = import.ReleaseDate,
                        IgdbId          = import.IgdbId,
                        CoverUrl        = import.CoverUrl,
                        DeveloperId     = await _cache.GetOrCreateDeveloperIdAsync(import.Developer),
                        PublisherId     = await _cache.GetOrCreatePublisherIdAsync(import.Publisher),
                        CreatedAt       = DateTime.UtcNow,
                        UpdatedAt       = DateTime.UtcNow,
                        Rating          = import.Rating,
                        RatingCount     = import.RatingCount,
                        IsDlc           = import.IsDlc,
                        ContentType     = import.ContentType
                    };

                    newGames.Add(game);
                    existingGames[game.NormalizedTitle!] = game;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed: {Title}", import.Title);
                job.ErrorCount++;
                await LogJobEventAsync(
                    job,
                    ImportJobLogLevel.Warning,
                    "importing",
                    $"Failed to process game '{import.Title}'",
                    new { import.Title, Exception = ex.Message },
                    ct);
            }
        }

        if (newGames.Count > 0)
        {
            var bulkConfig = new BulkConfig { SetOutputIdentity = true };
            await _db.BulkInsertAsync(newGames, bulkConfig, cancellationToken: ct);
            job.TotalGamesCreated = newGames.Count;

            // Rebuild existingGames with the new GameIds after bulk insert
            foreach (var game in newGames)
            {
                if (!string.IsNullOrEmpty(game.NormalizedTitle))
                    existingGames[game.NormalizedTitle] = game;
            }

            foreach (var import in games)
            {
                if (string.IsNullOrEmpty(import.NormalizedTitle)) continue;
                if (!existingGames.TryGetValue(import.NormalizedTitle, out var game)) continue;

                var genreCount = 0;
                foreach (var genreName in import.Genres.Where(g => !string.IsNullOrWhiteSpace(g)))
                {
                    var genreId = await _cache.GetOrCreateGenreIdAsync(genreName);
                    var genreKey = $"{game.GameId}:{genreId}";
                    
                    // Skip if this GameGenre already exists in DB
                    if (existingGameGenreSet.Contains(genreKey))
                    {
                        _logger.LogDebug("Skipping existing GameGenre: GameId={GameId}, GenreId={GenreId}", game.GameId, genreId);
                        continue;
                    }
                    
                    newGameGenres.Add(new GameGenre
                    {
                        GameId  = game.GameId,
                        GenreId = genreId
                    });
                    genreCount++;
                }
                
                if (genreCount > 0)
                {
                    _logger.LogInformation("Added {Count} new genres for game '{Title}' (GameId={GameId})", genreCount, import.Title, game.GameId);
                }

                ProcessOffers(game.GameId, import.Offers,
                    existingOffers, newOffers, offersToUpdate);
            }
        }

        if (gamesToUpdate.Count  > 0) await _db.BulkUpdateAsync(gamesToUpdate,  cancellationToken: ct);
        if (newGameGenres.Count  > 0)
        {
            // Remove duplicates within the batch
            var distinctGenres = newGameGenres
                .GroupBy(g => new { g.GameId, g.GenreId })
                .Select(g => g.First())
                .ToList();
            
            _logger.LogInformation("Inserting {Count} distinct GameGenre records", distinctGenres.Count);
            
            try
            {
                await _db.BulkInsertAsync(distinctGenres, cancellationToken: ct);
                _logger.LogInformation("Successfully inserted {Count} GameGenre records", distinctGenres.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk insert for GameGenre records failed, attempting per-record insert fallback.");
                foreach (var genre in distinctGenres)
                {
                    try
                    {
                        _db.GameGenres.Add(genre);
                        await _db.SaveChangesAsync(ct);
                    }
                    catch (Exception itemEx)
                    {
                        _db.Entry(genre).State = EntityState.Detached;
                        job.ErrorCount++;
                        await LogJobEventAsync(
                            job,
                            ImportJobLogLevel.Warning,
                            "importing",
                            $"Skipped GameGenre link GameId={genre.GameId}, GenreId={genre.GenreId}",
                            new { Exception = itemEx.Message },
                            ct);
                    }
                }
            }
        }
        if (newOffers.Count      > 0)
        {
            // Remove duplicates by (GameId, ShopId) - keep first
            var distinctOffers = newOffers
                .GroupBy(o => new { o.GameId, o.ShopId })
                .Select(g => g.First())
                .ToList();
            
            if (distinctOffers.Count < newOffers.Count)
            {
                _logger.LogWarning("Removed {Count} duplicate offers (same GameId+ShopId)", 
                    newOffers.Count - distinctOffers.Count);
            }
            
            try
            {
                await _db.BulkInsertAsync(distinctOffers, cancellationToken: ct);
                job.TotalOffersCreated = distinctOffers.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed bulk insert for offers. Falling back to per-offer insert.");
                foreach (var offer in distinctOffers)
                {
                    try
                    {
                        _db.GameOffers.Add(offer);
                        await _db.SaveChangesAsync(ct);
                        job.TotalOffersCreated++;
                    }
                    catch (Exception itemEx)
                    {
                        _db.Entry(offer).State = EntityState.Detached;
                        job.ErrorCount++;
                        await LogJobEventAsync(
                            job,
                            ImportJobLogLevel.Warning,
                            "importing",
                            $"Skipped offer for GameId={offer.GameId}, ShopId={offer.ShopId}",
                            new { Exception = itemEx.Message, offer.ExternalId },
                            ct);
                    }
                }
            }
        }
        if (offersToUpdate.Count > 0)
        {
            try
            {
                await _db.BulkUpdateAsync(offersToUpdate, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed bulk update for offers. Falling back to per-offer update.");
                foreach (var offer in offersToUpdate)
                {
                    try
                    {
                        _db.GameOffers.Update(offer);
                        await _db.SaveChangesAsync(ct);
                    }
                    catch (Exception itemEx)
                    {
                        _db.Entry(offer).State = EntityState.Detached;
                        job.ErrorCount++;
                        await LogJobEventAsync(
                            job,
                            ImportJobLogLevel.Warning,
                            "importing",
                            $"Skipped offer update GameOfferId={offer.GameOfferId}",
                            new { Exception = itemEx.Message, offer.ExternalId },
                            ct);
                    }
                }
            }
            var distinctUpdatedOffers = offersToUpdate
                .GroupBy(o => o.GameOfferId)
                .Select(g => g.First())
                .ToList();
            job.TotalOffersUpdated += distinctUpdatedOffers.Count;
            job.SteamOffersUpdated += distinctUpdatedOffers.Count(o => o.ShopId == ShopConstants.Steam);
            job.GogOffersUpdated += distinctUpdatedOffers.Count(o => o.ShopId == ShopConstants.Gog);
            job.EgsOffersUpdated += distinctUpdatedOffers.Count(o => o.ShopId == ShopConstants.EpicGames);
        }

        _cache.Clear();
    }

    private void ProcessOffers(
        int gameId,
        List<GameOfferImport> imports,
        Dictionary<string, GameOffer> existingOffers,
        List<GameOffer> newOffers,
        List<GameOffer> offersToUpdate)
    {
        foreach (var import in imports)
        {
            var key = $"{import.ShopId}:{import.ExternalId}";
            existingOffers.TryGetValue(key, out var existing);

            if (existing != null)
            {
                // Prices are managed by PriceSyncService — skip price updates here
            }
            else
            {
                var offer = new GameOffer
                {
                    GameId          = gameId,
                    ShopId          = import.ShopId,
                    ExternalId      = import.ExternalId,
                    CurrentPrice    = import.CurrentPrice ?? 0,
                    CurrentDiscount = import.CurrentDiscount ?? 0,
                    Currency        = import.Currency,
                    DownloadUrl     = ShopConstants.GetStoreUrl(import.ShopId, import.ExternalId),
                    // PriceSyncedAt left null — price will be set by PriceSyncService
                };
                newOffers.Add(offer);
                existingOffers[key] = offer;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        return new string(title.ToLower()
            .Replace(":", "").Replace("-", " ").Replace("'", "")
            .Replace("™", "").Replace("®", "").Replace("©", "")
            .Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray())
            .Trim().Replace("  ", " ");
    }

    private async Task LogJobEventAsync(
        ImportJob job,
        ImportJobLogLevel level,
        string phase,
        string message,
        object? data,
        CancellationToken ct)
    {
        _db.ImportJobLogs.Add(new ImportJobLog
        {
            ImportJobId = job.ImportJobId,
            Timestamp = DateTime.UtcNow,
            Level = level,
            Phase = phase,
            Message = message,
            Data = data == null ? null : JsonSerializer.Serialize(data)
        });
        await _db.SaveChangesAsync(ct);
    }

    private static ResiliencePipeline BuildExternalApiPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<Exception>()
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(20),
                ShouldHandle = new PredicateBuilder().Handle<Exception>()
            })
            .Build();
    }
}
