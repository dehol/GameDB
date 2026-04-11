using GameDB.Core.Configuration;
using GameDB.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameDB.Infrastructure.Services;

/// <summary>
/// RAWG API client – отримує повні метадані гри з retry logic
/// </summary>
public class RawgApiService : IRawgApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RawgApiService> _logger;
    private readonly RawgSettings _settings;
    private const int MaxRetries = 3;
    private const int MaxConcurrency = 10;

    public RawgApiService(HttpClient httpClient, ILogger<RawgApiService> logger, RawgSettings settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;
    }

    public async Task<List<RawgGame>> GetPopularGamesAsync(
        int limit = 1000,
        ISet<string>? excludeRawgIds = null,
        CancellationToken ct = default)
    {
        const string dates = "2020-01-01,2026-12-31";
        const string ordering = "-rating";
        const int pageSize = 40;

        _logger.LogInformation("📥 Starting RAWG import: {Limit} games with full metadata", limit);

        // Крок 1: Швидкий список (тільки ID)
        var basicList = await FetchBasicGameListAsync(dates, ordering, limit, pageSize, ct);

        // Крок 2: Паралельне отримання повних деталей
        var fullGames = await FetchFullDetailsInParallelAsync(basicList, excludeRawgIds, ct);

        _logger.LogInformation("✅ RAWG: received full metadata for {Count} games", fullGames.Count);
        return fullGames;
    }

    private async Task<List<RawgGame>> FetchBasicGameListAsync(
        string dates, string ordering, int limit, int pageSize, CancellationToken ct)
    {
        var games = new List<RawgGame>();
        var page = 1;
        var remaining = limit;

        while (remaining > 0)
        {
            var url = $"{_settings.ApiBaseUrl}/games?key={Uri.EscapeDataString(_settings.ApiKey)}" +
                      $"&dates={Uri.EscapeDataString(dates)}&ordering={ordering}" +
                      $"&page_size={Math.Min(remaining, pageSize)}&page={page}";

            var response = await ExecuteWithRetryAsync(() => 
                ExecuteRequestAsync<RawgListResponse<RawgGameResponse>>(url, ct), ct);
            
            if (response.Results.Count == 0) break;

            games.AddRange(response.Results.Select(MapToRawgGame));
            remaining -= response.Results.Count;
            page++;

            if (response.Next is null) break;
        }
        return games;
    }

    private async Task<List<RawgGame>> FetchFullDetailsInParallelAsync(
        List<RawgGame> basicList, ISet<string>? excludeIds, CancellationToken ct)
    {
        var semaphore = new SemaphoreSlim(MaxConcurrency);
        var result = new System.Collections.Concurrent.ConcurrentBag<RawgGame>();

        var tasks = basicList.Select(async basic =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                if (excludeIds?.Contains(basic.Id.ToString()) == true) return;

                var full = await GetGameDetailsWithRetryAsync(basic.Id, ct);
                result.Add(full ?? basic);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch details for game {Id}, using basic data", basic.Id);
                result.Add(basic);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return result.ToList();
    }

    public async Task<RawgGame?> GetGameDetailsAsync(int rawgId)
    {
        var url = $"{_settings.ApiBaseUrl}/games/{rawgId}?key={Uri.EscapeDataString(_settings.ApiKey)}";
        try
        {
            var response = await ExecuteRequestAsync<RawgGameResponse>(url);
            return MapToRawgGame(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAWG details failed for ID {Id}", rawgId);
            return null;
        }
    }

    private async Task<RawgGame?> GetGameDetailsWithRetryAsync(int rawgId, CancellationToken ct)
    {
        var url = $"{_settings.ApiBaseUrl}/games/{rawgId}?key={Uri.EscapeDataString(_settings.ApiKey)}";
        try
        {
            var response = await ExecuteWithRetryAsync(() => 
                ExecuteRequestAsync<RawgGameResponse>(url, ct), ct);
            return MapToRawgGame(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAWG details failed after retries for ID {Id}", rawgId);
            return null;
        }
    }

    public async Task<List<RawgGame>> SearchGamesAsync(string query, int limit = 500)
    {
        _logger.LogInformation("Searching RAWG for games: {Query}, limit: {Limit}", query, limit);
        
        var games = new List<RawgGame>();
        var page = 1;
        var remaining = limit;
        
        while (remaining > 0)
        {
            var pageSize = Math.Min(remaining, _settings.PageSize);
            var url = $"{_settings.ApiBaseUrl}/games?key={Uri.EscapeDataString(_settings.ApiKey)}" +
                      $"&search={Uri.EscapeDataString(query)}" +
                      $"&page_size={pageSize}&page={page}";

            var response = await ExecuteWithRetryAsync(() => 
                ExecuteRequestAsync<RawgListResponse<RawgGameResponse>>(url), CancellationToken.None);
            
            if (response.Results.Count == 0)
                break;

            games.AddRange(response.Results.Select(MapToRawgGame));
            remaining -= response.Results.Count;
            page++;

            if (response.Next == null)
                break;
        }

        _logger.LogInformation("Found {Count} games from RAWG search", games.Count);
        return games;
    }

    private async Task<T> ExecuteRequestAsync<T>(string url, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }) ?? throw new InvalidOperationException("Deserialization failed");
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                return await action();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxRetries - 1)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                _logger.LogWarning("Rate limited by RAWG API, retry {Attempt}/{Max} in {Delay}s", 
                    attempt + 1, MaxRetries, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogError("Rate limited by RAWG API after {Max} retries", MaxRetries);
                throw;
            }
        }

        throw new InvalidOperationException("Should not reach here");
    }

    private RawgGame MapToRawgGame(RawgGameResponse r)
    {
        int? steamId = null;
        var steamStore = r.Stores?.FirstOrDefault(s => 
            s.Store?.Slug?.Equals("steam", StringComparison.OrdinalIgnoreCase) == true);

        if (steamStore?.Url is not null)
        {
            var match = Regex.Match(steamStore.Url, @"/app/(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var parsedId))
            {
                steamId = parsedId;
            }
        }

        return new RawgGame
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.DescriptionRaw ?? r.Description,
            Released = r.Released,
            Genres = r.Genres ?? new(),
            Developers = r.Developers ?? new(),
            Publishers = r.Publishers ?? new(),
            SteamAppId = steamId
        };
    }

    private record RawgListResponse<T>(List<T> Results, string? Next);
    private record RawgGameResponse(
        int Id,
        string Name,
        string? DescriptionRaw,
        string? Description,
        string? Released,
        List<RawgGenre>? Genres,
        List<RawgDeveloper>? Developers,
        List<RawgPublisher>? Publishers,
        List<RawgStore>? Stores);
}
