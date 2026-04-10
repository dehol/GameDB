using GameDB.Core.Configuration;
using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace GameDB.Infrastructure.Services;

/// <summary>
/// Collects raw data from external APIs (RAWG, Steam) and stores in RawGameData table
/// </summary>
public class RawDataCollector : IRawDataCollector
{
    private readonly AppDbContext _db;
    private readonly IRawgApiService _rawgApi;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RawDataCollector> _logger;
    private readonly ImportSettings _settings;

    public RawDataCollector(
        AppDbContext db,
        IRawgApiService rawgApi,
        IHttpClientFactory httpClientFactory,
        ILogger<RawDataCollector> logger,
        ImportSettings settings)
    {
        _db = db;
        _rawgApi = rawgApi;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _settings = settings;
    }

    public async Task CollectRawDataAsync(ImportJob job, CancellationToken ct)
    {
        // Phase 1.1: Fetch from RAWG
        job.Status = "running";
        job.CurrentPhase = "collecting_rawg";
        await _db.SaveChangesAsync(ct);
        
        await CollectFromRawgAsync(job, ct);

        // Phase 1.2: Fetch Steam prices
        job.CurrentPhase = "collecting_steam";
        await _db.SaveChangesAsync(ct);
        
        await CollectSteamPricesAsync(job, ct);
    }

    private async Task CollectFromRawgAsync(ImportJob job, CancellationToken ct)
    {
        _logger.LogInformation("📥 Fetching up to {Limit} popular games from RAWG", _settings.IgdbPopularGamesLimit);

        // Local cache guard: never fetch/store RAWG games we've already seen.
        var existingRawgIdsList = await _db.RawGameData
            .Where(r => r.Source == "RAWG")
            .Select(r => r.ExternalId)
            .Distinct()
            .ToListAsync(ct);
        var existingRawgIds = new HashSet<string>(existingRawgIdsList);

        var rawgGames = await _rawgApi.GetPopularGamesAsync(
            _settings.IgdbPopularGamesLimit,
            existingRawgIds,
            ct);
        var rawgGamesCount = rawgGames.Count;
        job.SteamTotal = rawgGamesCount;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "📊 Fetched {Count} new games from RAWG ({CachedCount} already cached)",
            rawgGamesCount, existingRawgIds.Count);

        if (rawgGamesCount == 0)
        {
            _logger.LogInformation("No new RAWG games to import, using cached raw data only");
            return;
        }

        var processed = 0;
        foreach (var batch in rawgGames.Chunk(_settings.BatchSize))
        {
            var rawItems = new List<RawGameData>(batch.Length);
            foreach (var game in batch)
            {
                var externalId = game.Id.ToString();
                if (!existingRawgIds.Add(externalId))
                {
                    continue;
                }

                rawItems.Add(new RawGameData
                {
                    Source = "RAWG",
                    ExternalId = externalId,
                    RawJson = JsonSerializer.Serialize(game),
                    FetchedAt = DateTime.UtcNow
                });
            }

            if (rawItems.Count == 0)
            {
                continue;
            }

            _db.RawGameData.AddRange(rawItems);
            processed += rawItems.Count;
            job.SteamProcessed = processed;
            await _db.SaveChangesAsync(ct);

            if (_settings.EnableDetailedLogging && processed % 500 == 0)
            {
                _logger.LogInformation("📥 Collected {Processed}/{Total} RAWG games", processed, rawgGamesCount);
            }
        }

        _logger.LogInformation("✅ Collected {Count} RAWG games to RawGameData", processed);
    }

    private async Task CollectSteamPricesAsync(ImportJob job, CancellationToken ct)
    {
        // Get RAWG games with Steam IDs
        var rawRawgGames = await _db.RawGameData
            .Where(r => r.Source == "RAWG" && !r.Processed)
            .ToListAsync(ct);

        var steamAppIds = new List<string>();
        foreach (var raw in rawRawgGames)
        {
            try
            {
                var rawgGame = JsonSerializer.Deserialize<RawgGame>(raw.RawJson);
                if (rawgGame?.SteamAppId is int steamAppId)
                {
                    steamAppIds.Add(steamAppId.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize RAWG game {Id}", raw.RawGameDataId);
            }
        }

        if (steamAppIds.Count == 0)
        {
            _logger.LogInformation("No Steam IDs found in RAWG data, skipping Steam price collection");
            return;
        }

        _logger.LogInformation("💰 Fetching Steam prices for {Count} games", steamAppIds.Count);

        var client = _httpClientFactory.CreateClient();
        var semaphore = new SemaphoreSlim(_settings.MaxConcurrentApiCalls);
        var errors = 0;
        var fetched = 0;
        var steamRawData = new ConcurrentBag<RawGameData>();

        var tasks = steamAppIds.Chunk(_settings.SteamBatchSize).Select(async batch =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var appIdsString = string.Join(",", batch);
                var url = $"https://store.steampowered.com/api/appdetails?appids={appIdsString}&cc=us&filters=price_overview,genres,short_description,developers,publishers,release_date,type";

                var response = await client.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    Interlocked.Increment(ref errors);
                    _logger.LogWarning("Steam API returned {StatusCode} for batch", (int)response.StatusCode);
                    return;
                }

                var content = await response.Content.ReadAsStringAsync(ct);

                steamRawData.Add(new RawGameData
                {
                    Source = "Steam",
                    ExternalId = appIdsString,
                    RawJson = content,
                    FetchedAt = DateTime.UtcNow
                });

                var count = Interlocked.Increment(ref fetched);
                if (_settings.EnableDetailedLogging && count % 50 == 0)
                {
                    _logger.LogInformation("💰 Fetched Steam prices for {Fetched} batches", count);
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errors);
                _logger.LogError(ex, "Failed to fetch Steam prices for batch");
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        if (!steamRawData.IsEmpty)
        {
            _db.RawGameData.AddRange(steamRawData);
        }
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("✅ Collected Steam prices for {Fetched} batches, {Errors} errors", fetched, errors);
    }
}
