// ... using statements (ті самі)

public class RawDataCollector : IRawDataCollector
{
    // конструктор без змін

    public async Task CollectRawDataAsync(ImportJob job, CancellationToken ct)
    {
        job.Status = "running";
        job.CurrentPhase = "collecting_rawg";
        await _db.SaveChangesAsync(ct);

        await CollectFromRawgAsync(job, ct);

        job.CurrentPhase = "collecting_steam";
        await _db.SaveChangesAsync(ct);

        await CollectSteamPricesAsync(job, ct);
    }

    private async Task CollectFromRawgAsync(ImportJob job, CancellationToken ct)
    {
        var existingIds = new HashSet<string>(
            await _db.RawGameData
                .Where(r => r.Source == "RAWG")
                .Select(r => r.ExternalId)
                .ToListAsync(ct));

        var games = await _rawgApi.GetPopularGamesAsync(_settings.IgdbPopularGamesLimit, existingIds, ct);

        if (games.Count == 0) return;

        var entities = games.Select(g => new RawGameData
        {
            Source = "RAWG",
            ExternalId = g.Id.ToString(),
            RawJson = JsonSerializer.Serialize(g),
            FetchedAt = DateTime.UtcNow
        }).ToList();

        job.SteamTotal = entities.Count;
        _db.RawGameData.AddRange(entities);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("✅ RAWG: {Count} games with full metadata saved", entities.Count);
    }

    private async Task CollectSteamPricesAsync(ImportJob job, CancellationToken ct)
    {
        var steamIds = await _db.RawGameData
            .Where(r => r.Source == "RAWG" && !r.Processed)
            .Select(r => JsonSerializer.Deserialize<RawgGame>(r.RawJson)!.SteamAppId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value.ToString())
            .Distinct()
            .ToListAsync(ct);

        if (steamIds.Count == 0) return;

        _logger.LogInformation("💰 Steam prices: {Count} games", steamIds.Count);

        var client = _httpClientFactory.CreateClient();
        var semaphore = new SemaphoreSlim(_settings.MaxConcurrentApiCalls);
        var results = new ConcurrentBag<RawGameData>();

        await Task.WhenAll(steamIds.Chunk(_settings.SteamBatchSize).Select(async batch =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var url = $"https://store.steampowered.com/api/appdetails?appids={string.Join(",", batch)}&cc=us&filters=price_overview";
                var resp = await client.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) return;

                results.Add(new RawGameData
                {
                    Source = "Steam",
                    ExternalId = string.Join(",", batch),
                    RawJson = await resp.Content.ReadAsStringAsync(ct),
                    FetchedAt = DateTime.UtcNow
                });
            }
            finally { semaphore.Release(); }
        }));

        if (results.Count > 0)
        {
            _db.RawGameData.AddRange(results);
            await _db.SaveChangesAsync(ct);
        }
    }
}