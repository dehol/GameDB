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
        job.CurrentPhase = options.GameIds is { Count: > 0 } ? "collecting_targeted" : "collecting";
        await _db.SaveChangesAsync(ct);

        if (options.GameIds is { Count: > 0 })
        {
            var targetedOffers = await CollectTargetedOffersAsync(job, options, ct);
            job.SteamTotal = options.GameIds.Count;

            if (ct.IsCancellationRequested) return;

            job.CurrentPhase = "importing_targeted_offers";
            await _db.SaveChangesAsync(ct);

            await ImportTargetedOffersAsync(targetedOffers, job, ct);
        }
        else
        {
            var games = await CollectFromIgdbAsync(job, options, ct);
            job.SteamTotal = games.Count;

            if (ct.IsCancellationRequested) return;

            job.CurrentPhase = "enriching_prices";
            await _db.SaveChangesAsync(ct);

            // Skip Steam price enrichment - prices will be synced separately via PriceSyncWorker
            _logger.LogInformation("Skipping Steam price enrichment - will be synced separately");
            var enriched = games;

            if (ct.IsCancellationRequested) return;

            job.CurrentPhase = "importing";
            await _db.SaveChangesAsync(ct);

            await ImportToDatabaseAsync(enriched, job, options, ct);
        }

        job.Status = ImportJobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        job.CurrentPhase = "completed";
        await _db.SaveChangesAsync(ct);

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
            .Where(g => g.RawgId != null) // reusing RawgId column for IGDB ID
            .Select(g => g.RawgId!.Value)
            .ToListAsync(ct))
            .ToHashSet();

        var includeIgdbIds = options.IgdbGameIds is { Count: > 0 }
            ? options.IgdbGameIds.ToHashSet()
            : null;
        var excludeIgdbIds = options.OverwriteExisting ? null : existingIgdbIds;

        var igdbGames = await _igdb.GetPcGamesAsync(
            excludeIgdbIds: excludeIgdbIds,
            includeIgdbIds: includeIgdbIds,
            maxGames: options.Limit,
            ct: ct);

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
            RawgId     = g.Id,
            Offers     = BuildOffers(g),
            Rating     = g.Rating,
            RatingCount = g.RatingCount,
            IsDlc = g.IsDlc
        }).Where(g => !string.IsNullOrEmpty(g.NormalizedTitle)).ToList();

        // Filter out games with no store offers
        var gamesWithOffers = imports.Where(i => i.Offers.Count > 0).ToList();
        var skippedCount = imports.Count - gamesWithOffers.Count;
        if (skippedCount > 0)
        {
            _logger.LogInformation("IGDB: Skipped {Count} games with no store offers (Steam/GOG/EGS)", skippedCount);
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

    private async Task<List<(int gameId, List<GameOfferImport> offers)>> CollectTargetedOffersAsync(
        ImportJob job,
        ImportPipelineOptions options,
        CancellationToken ct)
    {
        var requestedGameIds = options.GameIds?.Distinct().ToList() ?? new List<int>();
        if (requestedGameIds.Count == 0)
            return new();

        var selectedGames = await _db.Games
            .AsNoTracking()
            .Where(g => requestedGameIds.Contains(g.GameId))
            .Select(g => new { g.GameId, g.RawgId })
            .ToListAsync(ct);

        var selectedByRawgId = selectedGames
            .Where(g => g.RawgId.HasValue)
            .ToDictionary(g => g.RawgId!.Value, g => g.GameId);

        var includeIgdbIds = selectedByRawgId.Keys.ToHashSet();
        var skippedWithoutRawg = selectedGames.Count - includeIgdbIds.Count;

        if (skippedWithoutRawg > 0)
        {
            _logger.LogWarning(
                "Targeted import: {Count} games skipped because they have no IGDB/RawgId mapping",
                skippedWithoutRawg);
            job.ErrorCount += skippedWithoutRawg;
        }

        if (includeIgdbIds.Count == 0)
            return new();

        var igdbGames = await _igdb.GetPcGamesAsync(
            includeIgdbIds: includeIgdbIds,
            ct: ct);

        var result = new List<(int gameId, List<GameOfferImport> offers)>();
        foreach (var igdbGame in igdbGames)
        {
            if (!selectedByRawgId.TryGetValue(igdbGame.Id, out var gameId))
                continue;

            var offers = BuildOffers(igdbGame);
            if (offers.Count == 0) continue;

            result.Add((gameId, offers));
        }

        _logger.LogInformation(
            "Targeted import: collected offers for {GamesWithOffers} of {RequestedGames} requested games",
            result.Count,
            requestedGameIds.Count);

        return result;
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
            .SelectMany(g => g.Offers.Select(o => o.ExternalId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct().ToList();

        var existingOffers = await _db.GameOffers.AsNoTracking()
            .Where(o => o.ExternalId != null && externalIds.Contains(o.ExternalId))
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
                        _logger.LogDebug("Skipping existing game '{Title}' - OverwriteExisting is false", import.Title);
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
                    existingGames[game.NormalizedTitle!] = game;
                }
            }
            catch (Exception ex)
            { _logger.LogWarning(ex, "Failed: {Title}", import.Title); job.ErrorCount++; }
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
            job.TotalOffersCreated = distinctOffers.Count;
        }
        if (offersToUpdate.Count > 0) await _db.BulkUpdateAsync(offersToUpdate, cancellationToken: ct);

        _cache.Clear();
    }

    private async Task ImportTargetedOffersAsync(
        List<(int gameId, List<GameOfferImport> offers)> targetedOffers,
        ImportJob job,
        CancellationToken ct)
    {
        if (targetedOffers.Count == 0)
            return;

        var externalIds = targetedOffers
            .SelectMany(g => g.offers.Select(o => o.ExternalId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var existingOffers = await _db.GameOffers
            .AsNoTracking()
            .Where(o => o.ExternalId != null && externalIds.Contains(o.ExternalId))
            .ToDictionaryAsync(o => $"{o.ShopId}:{o.ExternalId}", ct);

        var newOffers = new List<GameOffer>();
        var offersToUpdate = new List<GameOffer>();

        foreach (var item in targetedOffers)
        {
            ProcessOffers(item.gameId, item.offers, existingOffers, newOffers, offersToUpdate);
        }

        if (newOffers.Count > 0)
        {
            var distinctOffers = newOffers
                .GroupBy(o => new { o.GameId, o.ShopId })
                .Select(g => g.First())
                .ToList();

            await _db.BulkInsertAsync(distinctOffers, cancellationToken: ct);
            job.TotalOffersCreated = distinctOffers.Count;
        }

        if (offersToUpdate.Count > 0)
        {
            await _db.BulkUpdateAsync(offersToUpdate, cancellationToken: ct);
        }
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
                if (import.CurrentPrice.HasValue &&
                    (existing.CurrentPrice    != import.CurrentPrice.Value ||
                     existing.CurrentDiscount != (import.CurrentDiscount ?? 0)))
                {
                    existing.CurrentPrice    = import.CurrentPrice.Value;
                    existing.CurrentDiscount = import.CurrentDiscount ?? 0;
                    existing.PriceSyncedAt   = DateTime.UtcNow;
                    offersToUpdate.Add(existing);
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
}
