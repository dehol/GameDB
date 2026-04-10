using GameDB.Core.Configuration;
using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace GameDB.Infrastructure.Services;

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
        job.Status = "running";
        job.CurrentPhase = "collecting_rawg";
        await _db.SaveChangesAsync(ct);

        await CollectFromRawgAsync(job, ct);

        job.CurrentPhase = "collecting_steam";
        await _db.SaveChangesAsync(ct);

        await CollectSteamPricesAsync(job, ct);

        job.Status = "completed";
        await _db.SaveChangesAsync(ct);
    }

    private async Task CollectFromRawgAsync(ImportJob job, CancellationToken ct)
    {
        var existingIds = await _db.RawGameData
            .Where(r => r.Source == "RAWG")
            .Select(r => r.ExternalId)
            .ToHashSetAsync(ct);

        var games = await _rawgApi.GetPopularGamesAsync(
            _settings.IgdbPopularGamesLimit,
            existingIds,
            ct);

        if (games.Count == 0) return;

        var now = DateTime.UtcNow;

        var entities = games.Select(g => new RawGameData
        {
            Source = "RAWG",
            ExternalId = g.Id.ToString(),
            RawJson = JsonSerializer.Serialize(g),
            SteamAppId = g.SteamAppId, // 🔥 ключова оптимізація
            FetchedAt = now
        }).ToList();

        job.SteamTotal = entities.Count;

        await _db.RawGameData.AddRangeAsync(entities, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("✅ RAWG: {Count} games saved", entities.Count);
    }

    private async Task CollectSteamPricesAsync(ImportJob job, CancellationToken ct)
    {
        var steamIds = await _db.RawGameData
            .Where(r => r.Source == "RAWG"
                        && !r.Processed
                        && r.SteamAppId != null)
            .Select(r => r.SteamAppId!.Value)
            .Distinct()
            .ToListAsync(ct);

        if (steamIds.Count == 0) return;

        _logger.LogInformation("💰 Steam prices: {Count} games", steamIds.Count);

        var client = _httpClientFactory.CreateClient();

        var semaphore = new SemaphoreSlim(_settings.MaxConcurrentApiCalls);
        var results = new ConcurrentBag<RawGameData>();

        var batches = steamIds.Chunk(_settings.SteamBatchSize);

        await Task.WhenAll(batches.Select(async batch =>
        {
            await semaphore.WaitAsync(ct);

            try
            {
                var ids = string.Join(",", batch);

                var url = $"https://store.steampowered.com/api/appdetails" +
                          $"?appids={ids}&cc=us&filters=price_overview";

                using var resp = await client.GetAsync(url, ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Steam API failed for batch: {Batch}", ids);
                    return;
                }

                var json = await resp.Content.ReadAsStringAsync(ct);

                results.Add(new RawGameData
                {
                    Source = "Steam",
                    ExternalId = ids,
                    RawJson = json,
                    FetchedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Steam batch failed");
            }
            finally
            {
                semaphore.Release();
            }
        }));

        if (results.Count > 0)
        {
            await _db.RawGameData.AddRangeAsync(results, ct);
            await _db.SaveChangesAsync(ct);
        }

        // 🔥 Маркуємо як processed
        await _db.RawGameData
            .Where(r => r.Source == "RAWG" && !r.Processed && r.SteamAppId != null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Processed, true), ct);
    }
}