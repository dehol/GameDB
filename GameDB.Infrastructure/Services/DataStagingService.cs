public class DataStagingService : IDataStagingService
{
    // конструктор

    public async Task ProcessRawDataAsync(ImportJob job, CancellationToken ct)
    {
        job.CurrentPhase = "staging";
        await _db.SaveChangesAsync(ct);

        var rawItems = await _db.RawGameData
            .Where(r => !r.Processed)
            .OrderBy(r => r.Source)
            .ThenBy(r => r.FetchedAt)
            .ToListAsync(ct);

        var byNormalizedTitle = await _db.StagingGames.ToDictionaryAsync(s => s.NormalizedTitle, s => s, ct);
        var bySteamAppId = byNormalizedTitle.Values
            .Where(s => !string.IsNullOrEmpty(s.SteamAppId))
            .ToDictionary(s => s.SteamAppId!, s => s); // O(1) пошук для Steam

        _logger.LogInformation("🔄 Staging {Count} raw items", rawItems.Count);

        var newStaging = new List<StagingGame>();
        var errors = 0;

        foreach (var raw in rawItems)
        {
            try
            {
                if (raw.Source == "RAWG")
                {
                    var game = ProcessRawgData(raw, byNormalizedTitle);
                    if (game is not null)
                    {
                        newStaging.Add(game);
                        byNormalizedTitle[game.NormalizedTitle] = game;
                        if (!string.IsNullOrEmpty(game.SteamAppId))
                            bySteamAppId[game.SteamAppId] = game;
                    }
                }
                else if (raw.Source == "Steam")
                {
                    ProcessSteamPriceData(raw, bySteamAppId);
                }

                raw.Processed = true;
                raw.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                errors++;
                // ... обробка помилок (залиш як було)
            }
        }

        if (newStaging.Count > 0)
        {
            _db.StagingGames.AddRange(newStaging);
            await _db.SaveChangesAsync(ct);
        }

        job.ErrorCount += errors;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("✅ Staging completed: {New} new games, {Errors} errors", newStaging.Count, errors);
    }

    // ProcessRawgData, CreateStagingGameFromRawg, UpdateStagingGameFromRawg, NormalizeTitle, ParseRawgDate — залишаються майже без змін (можеш скопіювати з попередньої відповіді)
    
    private void ProcessSteamPriceData(RawGameData raw, Dictionary<string, StagingGame> bySteamId)
    {
        using var doc = JsonDocument.Parse(raw.RawJson);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var appId = prop.Name;
            if (!bySteamId.TryGetValue(appId, out var game)) continue;

            var data = prop.Value;
            if (!data.TryGetProperty("success", out var s) || !s.GetBoolean()) continue;
            if (!data.TryGetProperty("data", out var gameData)) continue;

            var (price, discount) = ExtractSteamPrice(gameData);
            game.SteamPrice = price;
            game.SteamDiscount = discount;
            game.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static (decimal? price, short? discount) ExtractSteamPrice(JsonElement data)
    {
        if (!data.TryGetProperty("price_overview", out var po)) return (null, null);
        var final = po.TryGetProperty("final", out var f) ? f.GetInt32() : 0;
        var initial = po.TryGetProperty("initial", out var i) ? i.GetInt32() : 0;

        return (final / 100m, initial > 0 ? (short)Math.Round((1d - (double)final / initial) * 100) : null);
    }
}