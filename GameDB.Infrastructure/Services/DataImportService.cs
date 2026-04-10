public class DataImportService : IDataImportService
{
    // конструктор

    public async Task ImportStagedDataAsync(ImportJob job, CancellationToken ct)
    {
        job.CurrentPhase = "importing";
        await _db.SaveChangesAsync(ct);
        await _cache.PreloadAsync();

        var staged = await _db.StagingGames
            .Where(s => !s.IsProcessed)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        _logger.LogInformation("📦 Importing {Count} staged games", staged.Count);

        foreach (var batch in staged.Chunk(_settings.BatchSize))
        {
            await ImportBatchAsync(batch.ToList(), job, ct);
        }

        _logger.LogInformation("✅ Import finished: {Games} games, {Offers} offers", 
            job.TotalGamesCreated, job.TotalOffersCreated);

        _cache.Clear();
    }

    private async Task ImportBatchAsync(List<StagingGame> batch, ImportJob job, CancellationToken ct)
    {
        var normalizedTitles = batch.Select(s => s.NormalizedTitle).Distinct().ToList();

        var existingGames = await _db.Games
            .Where(g => normalizedTitles.Contains(g.NormalizedTitle))
            .ToDictionaryAsync(g => g.NormalizedTitle!, ct);

        var existingOffers = await _db.GameOffers
            .Where(o => o.ShopId == ShopConstants.Steam && 
                        batch.Any(s => s.SteamAppId == o.ExternalId))
            .ToDictionaryAsync(o => o.ExternalId, ct);

        var gameGenreMap = await BuildGameGenreMapAsync(existingGames.Values.Select(g => g.GameId), ct);

        int gamesCreated = 0, offersCreated = 0;

        foreach (var s in batch)
        {
            if (string.IsNullOrWhiteSpace(s.NormalizedTitle)) continue;

            var (created, offers) = await ImportSingleGameAsync(s, existingGames, existingOffers, gameGenreMap, ct);
            if (created) gamesCreated++;
            offersCreated += offers;
        }

        job.TotalGamesCreated += gamesCreated;
        job.TotalOffersCreated += offersCreated;
        await _db.SaveChangesAsync(ct); // один раз на batch
    }

    // ImportSingleGameAsync, AddMissingGenresAsync, CreateOrUpdateOffer — залишаються, але CreateOffersAsync зроблено синхронним
    private int CreateOrUpdateOffers(int gameId, StagingGame staged, Dictionary<string, GameOffer> existingOffers)
    {
        int count = 0;
        if (!string.IsNullOrEmpty(staged.SteamAppId))
            if (CreateOrUpdateOffer(gameId, ShopConstants.Steam, staged.SteamAppId, staged.SteamPrice, staged.SteamDiscount, existingOffers))
                count++;

        return count;
    }

    // ... решта методів (CreateOrUpdateOffer, AddMissingGenresAsync) — без змін
}