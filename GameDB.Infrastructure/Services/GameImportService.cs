using EFCore.BulkExtensions;
using GameDB.Core.Configuration;
using GameDB.Core.Constants;
using GameDB.Core.DTOs;
using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using GameDB.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameDB.Infrastructure.Services;

public class GameImportService
{
    private readonly AppDbContext _db;
    private readonly IIgdbApiService _igdb;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ReferenceDataCache _cache;
    private readonly ILogger<GameImportService> _logger;
    private readonly ImportSettings _settings;

    public GameImportService(
        AppDbContext db,
        IIgdbApiService igdb,
        IHttpClientFactory httpFactory,
        ReferenceDataCache cache,
        ILogger<GameImportService> logger,
        ImportSettings settings)
    {
        _db = db;
        _igdb = igdb;
        _httpFactory = httpFactory;
        _cache = cache;
        _logger = logger;
        _settings = settings;
    }

    public async Task RunImportAsync(ImportJob job, CancellationToken ct)
    {
        await RunImportAsync(job, new ImportPipelineOptions(), ct);
    }

    public async Task RunImportAsync(ImportJob job, ImportPipelineOptions options, CancellationToken ct)
    {
        job.Status = ImportJobStatus.Running;
        job.CurrentPhase = "collecting";
        job.LastUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await ThrowIfCancelledAsync(job, ct);
        var games = await CollectFromIgdbAsync(job, options, ct);
        job.SteamTotal = games.Count;
        job.LastUpdatedAt = DateTime.UtcNow;

        await ThrowIfCancelledAsync(job, ct);

        job.CurrentPhase = "enriching_prices";
        await _db.SaveChangesAsync(ct);

        // Skip Steam price enrichment - prices will be synced separately via PriceSyncWorker
        _logger.LogInformation("Skipping Steam price enrichment - will be synced separately");
        var enriched = games;

        await ThrowIfCancelledAsync(job, ct);

        job.CurrentPhase = "importing";
        job.LastUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (enriched.Count == 0)
        {
            job.Status = ImportJobStatus.CompletedWithWarnings;
            job.CompletedAt = DateTime.UtcNow;
            job.LastUpdatedAt = DateTime.UtcNow;
            job.CurrentPhase = "completed_with_warnings";
            job.WarningMessage = BuildNoEligibleWarningMessage(job);
            await _db.SaveChangesAsync(ct);

            _logger.LogWarning("Import completed with warnings: {Warning}", job.WarningMessage);
            return;
        }

        await ImportToDatabaseAsync(enriched, job, options, ct);
        await ThrowIfCancelledAsync(job, ct);

        job.Status = ImportJobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        job.LastUpdatedAt = DateTime.UtcNow;
        job.CurrentPhase = "completed";
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Import completed: {Games} games, {Offers} offers, {Errors} errors",
            job.TotalGamesCreated, job.TotalOffersCreated, job.ErrorCount);
    }

    // ── Phase 1: Collect from IGDB ────────────────────────────────────────

    private async Task<List<GameImport>> CollectFromIgdbAsync(ImportJob job, ImportPipelineOptions options, CancellationToken ct)
    {
        var includeIgdbIds = options.IgdbGameIds is { Count: > 0 }
            ? options.IgdbGameIds.ToHashSet()
            : null;

        var igdbGames = await _igdb.GetPcGamesAsync(
            includeIgdbIds: includeIgdbIds,
            maxGames: options.Limit,
            ct: ct);

        job.IgdbCollected = igdbGames.Count;

        var candidateImports = new List<GameImport>();
        var normalizedTitleSet = new HashSet<string>(StringComparer.Ordinal);

        foreach (var game in igdbGames)
        {
            var normalizedTitle = NormalizeTitle(game.Name);
            if (string.IsNullOrEmpty(normalizedTitle))
                continue;

            var offers = BuildOffers(game);
            var hasAnyStoreUrl =
                !string.IsNullOrWhiteSpace(game.SteamUrl) ||
                !string.IsNullOrWhiteSpace(game.GogUrl) ||
                !string.IsNullOrWhiteSpace(game.EgsUrl);

            if (offers.Count == 0)
            {
                if (hasAnyStoreUrl)
                    job.SkippedInvalidStoreIds++;
                else
                    job.SkippedNoStoreOffers++;
                continue;
            }

            if (!normalizedTitleSet.Add(normalizedTitle))
            {
                job.SkippedDuplicateTitles++;
                continue;
            }

            candidateImports.Add(new GameImport
            {
                Title = game.Name,
                NormalizedTitle = normalizedTitle,
                Description = game.Summary,
                ReleaseDate = game.FirstReleaseDate.HasValue
                    ? DateOnly.FromDateTime(DateTimeOffset
                        .FromUnixTimeSeconds(game.FirstReleaseDate.Value).DateTime)
                    : null,
                Developer = game.Developer,
                Publisher = game.Publisher,
                Genres = game.Genres,
                RawgId = game.Id,
                Offers = offers,
                Rating = game.Rating,
                RatingCount = game.RatingCount,
                IsDlc = game.IsDlc
            });
        }

        var imports = candidateImports;
        if (!options.OverwriteExisting)
        {
            var idsToCheck = imports
                .Where(i => i.RawgId.HasValue)
                .Select(i => i.RawgId!.Value);
            var existingIds = await GetExistingRawgIdsAsync(idsToCheck, ct);
            imports = imports.Where(i =>
            {
                if (!i.RawgId.HasValue) return true;
                if (existingIds.Contains(i.RawgId.Value))
                {
                    job.SkippedAlreadyImported++;
                    return false;
                }

                return true;
            }).ToList();
        }

        if (options.Limit is > 0)
            imports = imports.Take(options.Limit.Value).ToList();

        job.EligibleForImport = imports.Count;

        _logger.LogInformation(
            "IGDB: collected={Collected}, eligible={Eligible}, skipped(no_offers={NoOffers}, invalid_store_ids={InvalidStoreIds}, already_imported={AlreadyImported}, duplicate_titles={DuplicateTitles})",
            job.IgdbCollected, job.EligibleForImport, job.SkippedNoStoreOffers, job.SkippedInvalidStoreIds, job.SkippedAlreadyImported, job.SkippedDuplicateTitles);

        var gamesWithGenres = imports.Where(i => i.Genres.Count > 0).ToList();
        _logger.LogInformation("IGDB: {Count} eligible games have genres", gamesWithGenres.Count);
        foreach (var import in gamesWithGenres.Take(5))
            _logger.LogInformation("Game '{Title}' genres: {Genres}", import.Title, string.Join(", ", import.Genres));

        job.GogTotal = imports.Count(g => g.Offers.Any(o => o.ShopId == ShopConstants.Gog));
        job.EgsTotal = imports.Count(g => g.Offers.Any(o => o.ShopId == ShopConstants.EpicGames));
        await _db.SaveChangesAsync(ct);

        return imports;
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

        if (g.EgsUrl != null)
        {
            var egsId = ExtractEgsId(g.EgsUrl);
            if (egsId != null)
                offers.Add(new GameOfferImport
                {
                    ShopId     = ShopConstants.EpicGames,
                    ExternalId = egsId
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

    private static string? ExtractEgsId(string url)
    {
        // EGS URLs: https://store.epicgames.com/en-US/p/game-slug
        var m = Regex.Match(url, @"epicgames\.com/.+?/p/([^/?#]+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    // ── Phase 2: Steam prices ─────────────────────────────────────────────

    private async Task<List<GameImport>> EnrichWithSteamPricesAsync(
        List<GameImport> games, CancellationToken ct)
    {
        var steamGames = games
            .Where(g => g.Offers.Any(o => o.ShopId == ShopConstants.Steam))
            .ToList();

        if (steamGames.Count == 0) return games;

        _logger.LogInformation("Fetching Steam prices for {Count} games", steamGames.Count);

        var client    = _httpFactory.CreateClient();
        var semaphore = new SemaphoreSlim(2);
        var priceMap  = new ConcurrentDictionary<string, (decimal? price, short? discount)>();

        var batches = steamGames
            .Select(g => g.Offers.First(o => o.ShopId == ShopConstants.Steam).ExternalId)
            .Distinct()
            .Chunk(_settings.SteamBatchSize);

        foreach (var batch in batches)
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var ids = string.Join(",", batch);
                var url = $"https://store.steampowered.com/api/appdetails" +
                          $"?appids={ids}&cc=us&filters=price_overview";
                using var resp = await client.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) continue;
                ParseSteamPrices(await resp.Content.ReadAsStringAsync(ct), priceMap);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Steam price batch failed"); }
            finally { semaphore.Release(); }
        }

        _logger.LogInformation("Got Steam prices for {Count} games", priceMap.Count);

        return games.Select(game =>
        {
            var offer = game.Offers.FirstOrDefault(o => o.ShopId == ShopConstants.Steam);
            if (offer == null || !priceMap.TryGetValue(offer.ExternalId, out var p))
                return game;

            return game with
            {
                Offers = game.Offers.Select(o =>
                    o.ShopId == ShopConstants.Steam
                        ? o with { CurrentPrice = p.price, CurrentDiscount = p.discount }
                        : o).ToList()
            };
        }).ToList();
    }

    private static void ParseSteamPrices(string json,
        ConcurrentDictionary<string, (decimal?, short?)> map)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var data = prop.Value;
            if (!data.TryGetProperty("success", out var s) || !s.GetBoolean()) continue;
            if (!data.TryGetProperty("data", out var gd)) continue;
            if (!gd.TryGetProperty("price_overview", out var po)) continue;

            var final   = po.TryGetProperty("final",   out var f) ? f.GetInt32() : 0;
            var initial = po.TryGetProperty("initial", out var i) ? i.GetInt32() : 0;

            map[prop.Name] = (
                final / 100m,
                initial > 0 ? (short)Math.Round((1d - (double)final / initial) * 100) : (short)0
            );
        }
    }

    // ── Phase 3: Import to DB ─────────────────────────────────────────────

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
            .SelectMany(g => g.Offers
                .Where(o => !string.IsNullOrWhiteSpace(o.ExternalId))
                .Select(o => o.ExternalId!))
            .Distinct().ToList();

        var existingOffers = await _db.GameOffers.AsNoTracking()
            .Where(o => o.ExternalId != null && externalIds.Contains(o.ExternalId))
            .ToDictionaryAsync(o => $"{o.ShopId}:{o.ExternalId}", ct);

        var newGames       = new List<Game>();
        var gamesToUpdate  = new List<Game>();
        var newOffers      = new List<GameOffer>();
        var offersToUpdate = new List<GameOffer>();
        var newGameGenres  = new List<GameGenre>();
        var counters = new ImportCounters();
        var processedSinceSave = 0;

        foreach (var import in games)
        {
            try
            {
                await ThrowIfCancelledAsync(job, ct);
                if (string.IsNullOrEmpty(import.NormalizedTitle)) continue;
                counters.GamesProcessed++;
                IncrementStoreProcessed(import, counters);

                if (existingGames.TryGetValue(import.NormalizedTitle, out var existing))
                {
                    if (!options.OverwriteExisting)
                    {
                        _logger.LogDebug("Skipping existing game '{Title}' - OverwriteExisting is false", import.Title);
                        counters.GamesSkipped++;
                        counters.OffersSkipped += import.Offers.Count;
                        IncrementStoreSkipped(import, counters);
                        processedSinceSave++;
                        await PersistProgressIfNeededAsync(job, counters, processedSinceSave, ct);
                        if (processedSinceSave >= 25) processedSinceSave = 0;
                        continue;
                    }

                    _logger.LogInformation("Updating existing game '{Title}' (GameId={GameId})", import.Title, existing.GameId);
                    
                    var needsUpdate = false;
                    var offersCreatedBefore = counters.OffersCreated;
                    var offersUpdatedBefore = counters.OffersUpdated;

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

                    if (needsUpdate)
                    {
                        existing.UpdatedAt = DateTime.UtcNow;
                        gamesToUpdate.Add(existing);
                    }

                    ProcessOffers(existing.GameId, import.Offers, existingOffers, newOffers, offersToUpdate, counters);

                    var hasOfferChanges =
                        counters.OffersCreated > offersCreatedBefore ||
                        counters.OffersUpdated > offersUpdatedBefore;

                    if (needsUpdate || hasOfferChanges)
                    {
                        counters.GamesUpdated++;
                    }
                    else
                    {
                        counters.GamesSkipped++;
                    }
                }
                else
                {
                    var game = new Game
                    {
                        Title           = import.Title,
                        NormalizedTitle = import.NormalizedTitle,
                        Description     = import.Description,
                        ReleaseDate     = import.ReleaseDate,
                        RawgId          = import.RawgId,
                        DeveloperId     = await _cache.GetOrCreateDeveloperIdAsync(import.Developer),
                        PublisherId     = await _cache.GetOrCreatePublisherIdAsync(import.Publisher),
                        CreatedAt       = DateTime.UtcNow,
                        UpdatedAt       = DateTime.UtcNow,
                        Rating          = import.Rating,
                        RatingCount     = import.RatingCount,
                        IsDlc           = import.IsDlc
                    };

                    newGames.Add(game);
                    counters.GamesCreated++;
                    existingGames[game.NormalizedTitle!] = game;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed: {Title}", import.Title);
                counters.GamesFailed++;
                counters.Errors++;
                counters.OffersFailed += import.Offers.Count;
                IncrementStoreFailed(import, counters);
            }

            processedSinceSave++;
            await PersistProgressIfNeededAsync(job, counters, processedSinceSave, ct);
            if (processedSinceSave >= 25) processedSinceSave = 0;
        }

        if (newGames.Count > 0)
        {
            var bulkConfig = new BulkConfig { SetOutputIdentity = true };
            await _db.BulkInsertAsync(newGames, bulkConfig, cancellationToken: ct);

            // Rebuild existingGames with the new GameIds after bulk insert
            var newNormalizedTitles = newGames
                .Where(g => !string.IsNullOrEmpty(g.NormalizedTitle))
                .Select(g => g.NormalizedTitle!)
                .ToHashSet();

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

                if (newNormalizedTitles.Contains(import.NormalizedTitle))
                    ProcessOffers(game.GameId, import.Offers, existingOffers, newOffers, offersToUpdate, counters);
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
                _logger.LogError(ex, "Failed to insert GameGenre records. Count={Count}", distinctGenres.Count);
                throw;
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
            
            await _db.BulkInsertAsync(distinctOffers, cancellationToken: ct);
        }
        if (offersToUpdate.Count > 0) await _db.BulkUpdateAsync(offersToUpdate, cancellationToken: ct);

        SyncCounters(job, counters);
        job.LastUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _cache.Clear();
    }

    private void ProcessOffers(
        int gameId,
        List<GameOfferImport> imports,
        Dictionary<string, GameOffer> existingOffers,
        List<GameOffer> newOffers,
        List<GameOffer> offersToUpdate,
        ImportCounters counters)
    {
        foreach (var import in imports)
        {
            var key = $"{import.ShopId}:{import.ExternalId}";
            existingOffers.TryGetValue(key, out var existing);

            if (existing != null)
            {
                if (import.CurrentPrice.HasValue &&
                    (existing.CurrentPrice    != import.CurrentPrice.Value ||
                     existing.CurrentDiscount != (import.CurrentDiscount ?? 0)))
                {
                    existing.CurrentPrice    = import.CurrentPrice.Value;
                    existing.CurrentDiscount = import.CurrentDiscount ?? 0;
                    existing.PriceSyncedAt   = DateTime.UtcNow;
                    offersToUpdate.Add(existing);
                    counters.OffersUpdated++;
                    IncrementStoreUpdated(import.ShopId, counters);
                }
                else
                {
                    counters.OffersSkipped++;
                    IncrementStoreSkipped(import.ShopId, counters);
                }
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
                    PriceSyncedAt   = DateTime.UtcNow
                };
                newOffers.Add(offer);
                existingOffers[key] = offer;
                counters.OffersCreated++;
                IncrementStoreNew(import.ShopId, counters);
            }
        }
    }

    private async Task ThrowIfCancelledAsync(ImportJob job, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var status = await _db.ImportJobs
            .Where(j => j.ImportJobId == job.ImportJobId)
            .Select(j => j.Status)
            .FirstOrDefaultAsync(ct);

        if (status == ImportJobStatus.Cancelled)
            throw new OperationCanceledException("Import pipeline was cancelled");
    }

    private async Task PersistProgressIfNeededAsync(
        ImportJob job,
        ImportCounters counters,
        int processedSinceSave,
        CancellationToken ct)
    {
        if (processedSinceSave < 25) return;
        SyncCounters(job, counters);
        job.LastUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static void SyncCounters(ImportJob job, ImportCounters c)
    {
        job.SteamProcessed = c.SteamProcessed;
        job.TotalGamesCreated = c.GamesCreated;
        job.TotalGamesUpdated = c.GamesUpdated;
        job.TotalGamesSkipped = c.GamesSkipped;
        job.TotalGamesFailed = c.GamesFailed;
        job.TotalOffersCreated = c.OffersCreated;
        job.TotalOffersUpdated = c.OffersUpdated;
        job.TotalOffersSkipped = c.OffersSkipped;
        job.TotalOffersFailed = c.OffersFailed;
        job.ErrorCount = c.Errors;
        job.SteamImported = c.SteamNew;
        job.SteamUpdated = c.SteamUpdated;
        job.SteamSkipped = c.SteamSkipped;
        job.SteamFailed = c.SteamFailed;
        job.GogImported = c.GogNew;
        job.GogUpdated = c.GogUpdated;
        job.GogSkipped = c.GogSkipped;
        job.GogFailed = c.GogFailed;
        job.EgsImported = c.EgsNew;
        job.EgsUpdated = c.EgsUpdated;
        job.EgsSkipped = c.EgsSkipped;
        job.EgsFailed = c.EgsFailed;
        job.GogProcessed = c.GogProcessed;
        job.EgsProcessed = c.EgsProcessed;
    }

    private static void IncrementStoreProcessed(GameImport import, ImportCounters counters)
    {
        foreach (var offer in import.Offers)
        {
            if (offer.ShopId == ShopConstants.Steam) counters.SteamProcessed++;
            else if (offer.ShopId == ShopConstants.Gog) counters.GogProcessed++;
            else if (offer.ShopId == ShopConstants.EpicGames) counters.EgsProcessed++;
        }
    }

    private static void IncrementStoreSkipped(GameImport import, ImportCounters counters)
    {
        foreach (var offer in import.Offers)
            IncrementStoreSkipped(offer.ShopId, counters);
    }

    private static void IncrementStoreFailed(GameImport import, ImportCounters counters)
    {
        foreach (var offer in import.Offers)
        {
            if (offer.ShopId == ShopConstants.Steam) counters.SteamFailed++;
            else if (offer.ShopId == ShopConstants.Gog) counters.GogFailed++;
            else if (offer.ShopId == ShopConstants.EpicGames) counters.EgsFailed++;
        }
    }

    private static void IncrementStoreNew(int shopId, ImportCounters counters)
    {
        if (shopId == ShopConstants.Steam) counters.SteamNew++;
        else if (shopId == ShopConstants.Gog) counters.GogNew++;
        else if (shopId == ShopConstants.EpicGames) counters.EgsNew++;
    }

    private static void IncrementStoreUpdated(int shopId, ImportCounters counters)
    {
        if (shopId == ShopConstants.Steam) counters.SteamUpdated++;
        else if (shopId == ShopConstants.Gog) counters.GogUpdated++;
        else if (shopId == ShopConstants.EpicGames) counters.EgsUpdated++;
    }

    private static void IncrementStoreSkipped(int shopId, ImportCounters counters)
    {
        if (shopId == ShopConstants.Steam) counters.SteamSkipped++;
        else if (shopId == ShopConstants.Gog) counters.GogSkipped++;
        else if (shopId == ShopConstants.EpicGames) counters.EgsSkipped++;
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

    private async Task<HashSet<int>> GetExistingRawgIdsAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var distinctIds = ids.Distinct().ToArray();
        var existing = new HashSet<int>();
        foreach (var chunk in distinctIds.Chunk(500))
        {
            var chunkMatches = await _db.Games
                .AsNoTracking()
                .Where(g => g.RawgId.HasValue && chunk.Contains(g.RawgId.Value))
                .Select(g => g.RawgId!.Value)
                .ToListAsync(ct);
            foreach (var id in chunkMatches)
                existing.Add(id);
        }

        return existing;
    }

    private static string BuildNoEligibleWarningMessage(ImportJob job) =>
        $"No eligible games to import. Collected: {job.IgdbCollected}, " +
        $"Skipped already imported: {job.SkippedAlreadyImported}, " +
        $"Skipped no store offers: {job.SkippedNoStoreOffers}, " +
        $"Skipped invalid store IDs: {job.SkippedInvalidStoreIds}, " +
        $"Skipped duplicate titles: {job.SkippedDuplicateTitles}.";

    private sealed class ImportCounters
    {
        public int GamesProcessed { get; set; }
        public int GamesCreated { get; set; }
        public int GamesUpdated { get; set; }
        public int GamesSkipped { get; set; }
        public int GamesFailed { get; set; }
        public int OffersCreated { get; set; }
        public int OffersUpdated { get; set; }
        public int OffersSkipped { get; set; }
        public int OffersFailed { get; set; }
        public int Errors { get; set; }
        public int SteamNew { get; set; }
        public int SteamProcessed { get; set; }
        public int SteamUpdated { get; set; }
        public int SteamSkipped { get; set; }
        public int SteamFailed { get; set; }
        public int GogNew { get; set; }
        public int GogUpdated { get; set; }
        public int GogSkipped { get; set; }
        public int GogFailed { get; set; }
        public int EgsNew { get; set; }
        public int EgsUpdated { get; set; }
        public int EgsSkipped { get; set; }
        public int EgsFailed { get; set; }
        public int GogProcessed { get; set; }
        public int EgsProcessed { get; set; }
    }
}
