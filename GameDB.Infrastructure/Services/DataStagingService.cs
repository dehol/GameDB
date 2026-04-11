using GameDB.Core.Configuration;
using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GameDB.Infrastructure.Services;

/// <summary>
/// Processes raw data from RawGameData table into StagingGame table
/// </summary>
public class DataStagingService : IDataStagingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DataStagingService> _logger;
    private readonly ImportSettings _settings;

    public DataStagingService(
        AppDbContext db,
        ILogger<DataStagingService> logger,
        ImportSettings settings)
    {
        _db = db;
        _logger = logger;
        _settings = settings;
    }

    public async Task ProcessRawDataAsync(ImportJob job, CancellationToken ct)
    {
        job.CurrentPhase = "staging";
        await _db.SaveChangesAsync(ct);

        var rawItems = await _db.RawGameData
            .AsNoTracking() // Оптимізація: read-only query
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
                raw.ProcessingAttempts++;
                raw.ProcessingError = ex.Message;

                if (raw.ProcessingAttempts >= _settings.MaxRetries)
                    raw.Processed = true;
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
	private StagingGame? ProcessRawgData(RawGameData raw, Dictionary<string, StagingGame> existing)
    {
        var rawgGame = JsonSerializer.Deserialize<RawgGame>(raw.RawJson);
        if (rawgGame == null) return null;

        var normalized = NormalizeTitle(rawgGame.Name);

        if (existing.TryGetValue(normalized, out var existingStaging))
        {
            UpdateStagingGameFromRawg(existingStaging, rawgGame);
            return null;
        }

        return CreateStagingGameFromRawg(rawgGame, normalized);
    }
	private static StagingGame CreateStagingGameFromRawg(RawgGame rawg, string normalizedTitle)
    {
        return new StagingGame
        {
            NormalizedTitle = normalizedTitle,
            Title = rawg.Name,
            IgdbId = rawg.Id.ToString(),
            SteamAppId = rawg.SteamAppId?.ToString(),
            Description = rawg.Description,
            ReleaseDate = ParseRawgDate(rawg.Released),
            Developer = rawg.Developers.FirstOrDefault()?.Name,
            Publisher = rawg.Publishers.FirstOrDefault()?.Name,
            Genres = rawg.Genres.Select(g => g.Name).ToList(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
private static void UpdateStagingGameFromRawg(StagingGame staging, RawgGame rawg)
    {
        staging.IgdbId = rawg.Id.ToString();
        staging.Description = rawg.Description;
        staging.ReleaseDate = ParseRawgDate(rawg.Released);
        staging.Developer = rawg.Developers.FirstOrDefault()?.Name;
        staging.Publisher = rawg.Publishers.FirstOrDefault()?.Name;
        staging.Genres = rawg.Genres.Select(g => g.Name).ToList();
        staging.SteamAppId = rawg.SteamAppId?.ToString();
        staging.UpdatedAt = DateTime.UtcNow;
    }
private static DateOnly? ParseRawgDate(string? date) =>
        DateOnly.TryParse(date, out var d) ? d : null;

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