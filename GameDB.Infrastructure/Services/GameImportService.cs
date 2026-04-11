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

namespace GameDB.Infrastructure.Services;

/// <summary>
/// Unified game import service - collects from RAWG, enriches with prices, imports to DB
/// Replaces: RawDataCollector, DataStagingService, DataImportService
/// </summary>
public class GameImportService
{
    private readonly AppDbContext _db;
    private readonly IRawgApiService _rawgApi;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ReferenceDataCache _cache;
    private readonly ILogger<GameImportService> _logger;
    private readonly ImportSettings _settings;

    public GameImportService(
        AppDbContext db,
        IRawgApiService rawgApi,
        IHttpClientFactory httpFactory,
        ReferenceDataCache cache,
        ILogger<GameImportService> logger,
        ImportSettings settings)
    {
        _db = db;
        _rawgApi = rawgApi;
        _httpFactory = httpFactory;
        _cache = cache;
        _logger = logger;
        _settings = settings;
    }

    /// <summary>
    /// Main import pipeline - fetch, enrich, import
    /// </summary>
    public async Task RunImportAsync(ImportJob job, CancellationToken ct)
    {
        job.Status = ImportJobStatus.Running;
        job.CurrentPhase = "collecting";
        await _db.SaveChangesAsync(ct);

        // Phase 1: Collect from RAWG
        var games = await CollectFromRawgAsync(job, ct);
        job.SteamTotal = games.Count;

        if (ct.IsCancellationRequested) return;

        // Phase 2: Enrich with prices
        job.CurrentPhase = "enriching_prices";
        await _db.SaveChangesAsync(ct);

        var enrichedGames = await EnrichWithSteamPricesAsync(games, job, ct);

        if (ct.IsCancellationRequested) return;

        // Phase 3: Import to DB
        job.CurrentPhase = "importing";
        await _db.SaveChangesAsync(ct);

        await ImportToDatabaseAsync(enrichedGames, job, ct);

        // Done
        job.Status = ImportJobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        job.CurrentPhase = "completed";
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "✅ Import completed: {Games} games, {Offers} offers, {Errors} errors",
            job.TotalGamesCreated, job.TotalOffersCreated, job.ErrorCount);
    }

    #region Phase 1: Collect from RAWG

    private async Task<List<GameImport>> CollectFromRawgAsync(ImportJob job, CancellationToken ct)
    {
        _logger.LogInformation("📥 Fetching games from RAWG API...");

        var existingRawgIds = (await _db.Games
            .AsNoTracking()
            .Where(g => g.RawgId != null)
            .Select(g => g.RawgId!.Value.ToString())
            .ToListAsync(ct))
            .ToHashSet();

        var rawgGames = await _rawgApi.GetPopularGamesAsync(
            _settings.IgdbPopularGamesLimit,
            existingRawgIds,
            ct);

        _logger.LogInformation("📥 RAWG returned {Count} games", rawgGames.Count);

        // Map to GameImport
        return rawgGames.Select(g => new GameImport
        {
            Title = g.Name,
            NormalizedTitle = NormalizeTitle(g.Name),
            Description = g.Description,
            ReleaseDate = ParseDate(g.Released),
            Developer = g.Developers.FirstOrDefault()?.Name,
            Publisher = g.Publishers.FirstOrDefault()?.Name,
            Genres = g.Genres.Select(x => x.Name).ToList(),
            RawgId = g.Id,
            Offers = g.SteamAppId.HasValue
                ? new List<GameOfferImport>
                {
                    new() { ShopId = ShopConstants.Steam, ExternalId = g.SteamAppId.Value.ToString() }
                }
                : new List<GameOfferImport>()
        }).ToList();
    }

    #endregion

    #region Phase 2: Enrich with Prices

    private async Task<List<GameImport>> EnrichWithSteamPricesAsync(List<GameImport> games, ImportJob job, CancellationToken ct)
    {
        var steamGames = games
            .Where(g => g.Offers.Any(o => o.ShopId == ShopConstants.Steam))
            .ToList();

        if (steamGames.Count == 0) return games;

        _logger.LogInformation("💰 Fetching Steam prices for {Count} games...", steamGames.Count);

        var client = _httpFactory.CreateClient();
        var semaphore = new SemaphoreSlim(2);
        var priceMap = new ConcurrentDictionary<string, (decimal? price, short? discount)>();

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
                var url = $"https://store.steampowered.com/api/appdetails?appids={ids}&cc=us&filters=price_overview";

                using var resp = await client.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Steam API failed for batch");
                    continue;
                }

                var json = await resp.Content.ReadAsStringAsync(ct);
                ParseSteamPrices(json, priceMap);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Steam price fetch failed");
            }
            finally
            {
                semaphore.Release();
            }
        }

        // Apply prices to games - return new list with updated prices
        var result = games.Select(game =>
        {
            var steamOffer = game.Offers.FirstOrDefault(o => o.ShopId == ShopConstants.Steam);
            if (steamOffer == null) return game;

            if (priceMap.TryGetValue(steamOffer.ExternalId, out var priceData))
            {
                var updatedOffers = game.Offers.Select(o =>
                    o.ShopId == ShopConstants.Steam
                        ? o with { CurrentPrice = priceData.price, CurrentDiscount = priceData.discount }
                        : o).ToList();

                return game with { Offers = updatedOffers };
            }

            return game;
        }).ToList();

        _logger.LogInformation("💰 Fetched prices for {Count} Steam games", priceMap.Count);
        return result;
    }

    private static void ParseSteamPrices(string json, ConcurrentDictionary<string, (decimal?, short?)> map)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var appId = prop.Name;
            var data = prop.Value;

            if (!data.TryGetProperty("success", out var s) || !s.GetBoolean()) continue;
            if (!data.TryGetProperty("data", out var gameData)) continue;
            if (!gameData.TryGetProperty("price_overview", out var po)) continue;

            var final = po.TryGetProperty("final", out var f) ? f.GetInt32() : 0;
            var initial = po.TryGetProperty("initial", out var i) ? i.GetInt32() : 0;

            var price = final / 100m;
            var discount = initial > 0 ? (short)Math.Round((1d - (double)final / initial) * 100) : (short)0;

            map[appId] = (price, discount);
        }
    }

    #endregion

    #region Phase 3: Import to Database

    private async Task ImportToDatabaseAsync(List<GameImport> games, ImportJob job, CancellationToken ct)
    {
        _logger.LogInformation("📦 Importing {Count} games to database...", games.Count);

        await _cache.PreloadAsync();

        // Get existing games for matching - filter out null normalized titles
        var normalizedTitles = games
            .Select(g => g.NormalizedTitle)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .ToList();
        
        var existingGames = (await _db.Games
            .AsNoTracking()
            .Where(g => g.NormalizedTitle != null && normalizedTitles.Contains(g.NormalizedTitle))
            .ToListAsync(ct))
            .Where(g => g.NormalizedTitle != null)
            .ToDictionary(g => g.NormalizedTitle!);

        // Get existing offers
        var externalIds = games
            .SelectMany(g => g.Offers.Select(o => o.ExternalId))
            .Distinct()
            .ToList();
        
        var existingOffers = await _db.GameOffers
            .AsNoTracking()
            .Where(o => externalIds.Contains(o.ExternalId))
            .ToDictionaryAsync(o => $"{o.ShopId}:{o.ExternalId}", ct);

        var newGames = new List<Game>();
        var gamesToUpdate = new List<Game>();
        var newOffers = new List<GameOffer>();
        var offersToUpdate = new List<GameOffer>();
        var newGameGenres = new List<GameGenre>();

        foreach (var import in games)
        {
            try
            {
                if (string.IsNullOrEmpty(import.NormalizedTitle)) continue;

                if (existingGames.TryGetValue(import.NormalizedTitle, out var existing))
                {
                    // Update existing game
                    var needsUpdate = false;

                    if (!string.IsNullOrEmpty(import.Description) && import.Description != existing.Description)
                    {
                        existing.Description = import.Description;
                        needsUpdate = true;
                    }
                    if (import.ReleaseDate.HasValue && import.ReleaseDate != existing.ReleaseDate)
                    {
                        existing.ReleaseDate = import.ReleaseDate;
                        needsUpdate = true;
                    }

                    var devId = await _cache.GetOrCreateDeveloperIdAsync(import.Developer);
                    var pubId = await _cache.GetOrCreatePublisherIdAsync(import.Publisher);

                    if (devId.HasValue && existing.DeveloperId != devId)
                    {
                        existing.DeveloperId = devId;
                        needsUpdate = true;
                    }
                    if (pubId.HasValue && existing.PublisherId != pubId)
                    {
                        existing.PublisherId = pubId;
                        needsUpdate = true;
                    }

                    if (needsUpdate)
                    {
                        existing.UpdatedAt = DateTime.UtcNow;
                        gamesToUpdate.Add(existing);
                    }

                    // Process offers
                    ProcessOffers(existing.GameId, import.Offers, existingOffers, newOffers, offersToUpdate);
                }
                else
                {
                    // Create new game
                    var game = new Game
                    {
                        Title = import.Title,
                        NormalizedTitle = import.NormalizedTitle,
                        Description = import.Description,
                        ReleaseDate = import.ReleaseDate,
                        RawgId = import.RawgId,
                        DeveloperId = await _cache.GetOrCreateDeveloperIdAsync(import.Developer),
                        PublisherId = await _cache.GetOrCreatePublisherIdAsync(import.Publisher),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    newGames.Add(game);
                    existingGames[game.NormalizedTitle] = game;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process game: {Title}", import.Title);
                job.ErrorCount++;
            }
        }

        // Bulk operations
        if (newGames.Count > 0)
        {
            _logger.LogInformation("📦 Bulk inserting {Count} new games", newGames.Count);
            await _db.BulkInsertAsync(newGames, cancellationToken: ct);
            job.TotalGamesCreated = newGames.Count;

            // Add genres for new games
            foreach (var import in games)
            {
                if (string.IsNullOrEmpty(import.NormalizedTitle)) continue;
                if (!existingGames.TryGetValue(import.NormalizedTitle, out var game)) continue;
                if (newGames.All(g => g.NormalizedTitle != import.NormalizedTitle)) continue;

                foreach (var genreName in import.Genres.Where(g => !string.IsNullOrWhiteSpace(g)))
                {
                    var genreId = await _cache.GetOrCreateGenreIdAsync(genreName!);
                    newGameGenres.Add(new GameGenre { GameId = game.GameId, GenreId = genreId });
                }

                ProcessOffers(game.GameId, import.Offers, existingOffers, newOffers, offersToUpdate);
            }
        }

        if (gamesToUpdate.Count > 0)
        {
            _logger.LogInformation("📦 Bulk updating {Count} existing games", gamesToUpdate.Count);
            await _db.BulkUpdateAsync(gamesToUpdate, cancellationToken: ct);
        }

        if (newGameGenres.Count > 0)
        {
            _logger.LogInformation("📦 Bulk inserting {Count} genre links", newGameGenres.Count);
            await _db.BulkInsertAsync(newGameGenres, cancellationToken: ct);
        }

        if (newOffers.Count > 0)
        {
            _logger.LogInformation("📦 Bulk inserting {Count} new offers", newOffers.Count);
            await _db.BulkInsertAsync(newOffers, cancellationToken: ct);
            job.TotalOffersCreated = newOffers.Count;
        }

        if (offersToUpdate.Count > 0)
        {
            _logger.LogInformation("📦 Bulk updating {Count} existing offers", offersToUpdate.Count);
            await _db.BulkUpdateAsync(offersToUpdate, cancellationToken: ct);
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
                if (import.CurrentPrice.HasValue &&
                    (existing.CurrentPrice != import.CurrentPrice.Value ||
                     existing.CurrentDiscount != (import.CurrentDiscount ?? 0)))
                {
                    existing.CurrentPrice = import.CurrentPrice.Value;
                    existing.CurrentDiscount = import.CurrentDiscount ?? 0;
                    existing.PriceSyncedAt = DateTime.UtcNow;
                    offersToUpdate.Add(existing);
                }
            }
            else
            {
                var offer = new GameOffer
                {
                    GameId = gameId,
                    ShopId = import.ShopId,
                    ExternalId = import.ExternalId,
                    CurrentPrice = import.CurrentPrice ?? 0,
                    CurrentDiscount = import.CurrentDiscount ?? 0,
                    Currency = import.Currency,
                    DownloadUrl = ShopConstants.GetStoreUrl(import.ShopId, import.ExternalId),
                    PriceSyncedAt = DateTime.UtcNow
                };

                newOffers.Add(offer);
                existingOffers[key] = offer;
            }
        }
    }

    #endregion

    #region Helpers

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        return new string(title.ToLower()
            .Replace(":", "").Replace("-", " ").Replace("'", "")
            .Replace("™", "").Replace("®", "").Replace("©", "")
            .Where(c => char.IsLetterOrDigit(c) || c == ' ')
            .ToArray())
            .Trim().Replace("  ", " ");
    }

    private static DateOnly? ParseDate(string? date) =>
        DateOnly.TryParse(date, out var d) ? d : null;

    #endregion
}
