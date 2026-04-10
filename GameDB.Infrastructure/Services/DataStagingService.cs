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
            .Where(r => !r.Processed)
            .OrderBy(r => r.Source)
            .ThenBy(r => r.FetchedAt)
            .ToListAsync(ct);

        _logger.LogInformation("🔄 Processing {Count} raw data items to staging", rawItems.Count);
        var existingStagingGames = await _db.StagingGames.ToListAsync(ct);
        var newStagingGames = new List<StagingGame>();
        var errors = 0;
        foreach (var raw in rawItems)
        {
            try
            {
                var stagingGame = ProcessRawItem(raw, existingStagingGames);
                if (stagingGame != null)
                {
                    newStagingGames.Add(stagingGame);
                    existingStagingGames.Add(stagingGame);
                }

                raw.Processed = true;
                raw.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                errors++;
                raw.ProcessingAttempts++;
                raw.ProcessingError = $"{ex.GetType().Name}: {ex.Message}";

                if (raw.ProcessingAttempts >= _settings.MaxRetries)
                {
                    raw.Processed = true;
                    _logger.LogError(ex, "Failed to process raw item {Id} after {Attempts} attempts",
                        raw.RawGameDataId, raw.ProcessingAttempts);
                }
            }
        }

        if (newStagingGames.Count > 0)
        {
            job.CurrentPhase = "saving_staging";
            _db.StagingGames.AddRange(newStagingGames);

            _logger.LogInformation("✅ Saved {Count} games to StagingGame, {Errors} errors",
                newStagingGames.Count, errors);
        }

        job.ErrorCount += errors;
        await _db.SaveChangesAsync(ct);
    }

    private StagingGame? ProcessRawItem(
        RawGameData raw,
        List<StagingGame> existingStagingGames)
    {
        if (raw.Source == "RAWG")
        {
            return ProcessRawgData(raw, existingStagingGames);
        }
        else if (raw.Source == "Steam")
        {
            ProcessSteamPriceData(raw, existingStagingGames);
            return null;
        }

        return null;
    }

    private StagingGame? ProcessRawgData(
        RawGameData raw,
        List<StagingGame> existingStagingGames)
    {
        var rawgGame = JsonSerializer.Deserialize<RawgGame>(raw.RawJson);
        if (rawgGame == null) return null;

        var normalizedTitle = NormalizeTitle(rawgGame.Name);
        var existingStaging = existingStagingGames
            .FirstOrDefault(s => s.NormalizedTitle == normalizedTitle);

        if (existingStaging != null)
        {
            UpdateStagingGameFromRawg(existingStaging, rawgGame);
            return null;
        }

        // Create new
        return CreateStagingGameFromRawg(rawgGame, normalizedTitle);
    }

    private void ProcessSteamPriceData(
        RawGameData raw,
        List<StagingGame> existingStagingGames)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw.RawJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var appId = prop.Name;
                var appData = prop.Value;

                if (!appData.TryGetProperty("success", out var successEl) || !successEl.GetBoolean())
                    continue;

                if (!appData.TryGetProperty("data", out var data))
                    continue;

                if (data.TryGetProperty("type", out var typeEl) &&
                    !string.Equals(typeEl.GetString(), "game", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (price, discount) = ExtractSteamPrice(data);
                var matchingGame = existingStagingGames.FirstOrDefault(s => s.SteamAppId == appId);

                if (matchingGame != null)
                {
                    matchingGame.SteamPrice = price;
                    matchingGame.SteamDiscount = discount;
                    matchingGame.Description = MergeDescription(
                        matchingGame.Description,
                        ExtractSteamShortDescription(data));
                    matchingGame.Developer ??= ExtractFirstStringFromArray(data, "developers");
                    matchingGame.Publisher ??= ExtractFirstStringFromArray(data, "publishers");
                    matchingGame.ReleaseDate ??= ExtractSteamReleaseDate(data);
                    matchingGame.Genres = MergeGenres(
                        matchingGame.Genres,
                        ExtractSteamGenres(data));
                    matchingGame.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process Steam price data");
        }
    }

    private static (decimal? price, short? discount) ExtractSteamPrice(JsonElement data)
    {
        decimal? price = null;
        short? discount = null;

        if (data.TryGetProperty("price_overview", out var priceObj))
        {
            var finalCents = priceObj.TryGetProperty("final", out var finalEl) ? finalEl.GetInt32() : 0;
            var initialCents = priceObj.TryGetProperty("initial", out var initialEl) ? initialEl.GetInt32() : 0;

            price = finalCents / 100m;
            if (initialCents > 0)
            {
                discount = (short)Math.Round((1 - (double)finalCents / initialCents) * 100);
            }
        }

        return (price, discount);
    }

    private static string? ExtractSteamShortDescription(JsonElement data) =>
        data.TryGetProperty("short_description", out var descEl)
            ? descEl.GetString()
            : null;

    private static string? ExtractFirstStringFromArray(JsonElement data, string propertyName)
    {
        if (!data.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in arr.EnumerateArray())
        {
            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static DateOnly? ExtractSteamReleaseDate(JsonElement data)
    {
        if (!data.TryGetProperty("release_date", out var releaseEl) ||
            !releaseEl.TryGetProperty("date", out var dateEl))
            return null;

        var dateString = dateEl.GetString();
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        return DateOnly.TryParse(dateString, out var date) ? date : null;
    }

    private static List<string> ExtractSteamGenres(JsonElement data)
    {
        var genres = new List<string>();
        if (!data.TryGetProperty("genres", out var genresEl) || genresEl.ValueKind != JsonValueKind.Array)
            return genres;

        foreach (var genre in genresEl.EnumerateArray())
        {
            if (genre.TryGetProperty("description", out var descEl))
            {
                var name = descEl.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                    genres.Add(name);
            }
        }

        return genres;
    }

    private static List<string> MergeGenres(List<string>? currentGenres, List<string> incomingGenres)
    {
        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (currentGenres != null)
        {
            foreach (var genre in currentGenres)
            {
                if (!string.IsNullOrWhiteSpace(genre))
                    merged.Add(genre.Trim());
            }
        }

        foreach (var genre in incomingGenres)
        {
            if (!string.IsNullOrWhiteSpace(genre))
                merged.Add(genre.Trim());
        }

        return merged.ToList();
    }

    private static string? MergeDescription(string? currentDescription, string? incomingDescription)
    {
        if (string.IsNullOrWhiteSpace(incomingDescription))
            return currentDescription;

        if (string.IsNullOrWhiteSpace(currentDescription))
            return incomingDescription;

        return incomingDescription.Length > currentDescription.Length
            ? incomingDescription
            : currentDescription;
    }

    private static void UpdateStagingGameFromRawg(StagingGame staging, RawgGame rawgGame)
    {
        staging.IgdbId = rawgGame.Id.ToString(); // Reuse IgdbId field for RAWG ID
        staging.Description = rawgGame.Description;
        staging.ReleaseDate = ParseRawgDate(rawgGame.Released);
        staging.Developer = rawgGame.Developers.FirstOrDefault()?.Name;
        staging.Publisher = rawgGame.Publishers.FirstOrDefault()?.Name;
        staging.Genres = rawgGame.Genres.Select(g => g.Name).ToList();
        staging.UpdatedAt = DateTime.UtcNow;

        if (rawgGame.SteamAppId.HasValue)
        {
            staging.SteamAppId = rawgGame.SteamAppId.ToString();
        }
    }

    private static StagingGame CreateStagingGameFromRawg(RawgGame rawgGame, string normalizedTitle)
    {
        return new StagingGame
        {
            NormalizedTitle = normalizedTitle,
            IgdbId = rawgGame.Id.ToString(), // Reuse IgdbId field for RAWG ID
            SteamAppId = rawgGame.SteamAppId?.ToString(),
            Title = rawgGame.Name,
            Description = rawgGame.Description,
            ReleaseDate = ParseRawgDate(rawgGame.Released),
            Developer = rawgGame.Developers.FirstOrDefault()?.Name,
            Publisher = rawgGame.Publishers.FirstOrDefault()?.Name,
            Genres = rawgGame.Genres.Select(g => g.Name).ToList(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static DateOnly? ParseRawgDate(string? dateString)
    {
        if (string.IsNullOrEmpty(dateString)) return null;
        return DateOnly.TryParse(dateString, out var date) ? date : null;
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "";
        }

        return new string(title.ToLower()
            .Replace(":", "")
            .Replace("-", " ")
            .Replace("'", "")
            .Replace("™", "")
            .Replace("®", "")
            .Replace("©", "")
            .Where(c => char.IsLetterOrDigit(c) || c == ' ')
            .ToArray())
            .Trim()
            .Replace("  ", " ");
    }
}
