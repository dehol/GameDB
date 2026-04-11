using GameDB.Core.Configuration;
using GameDB.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameDB.Infrastructure.Services;

/// <summary>
/// RAWG API client – отримує повні метадані гри
/// </summary>
public class RawgApiService : IRawgApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RawgApiService> _logger;
    private readonly RawgSettings _settings;

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

            var response = await ExecuteRequestAsync<RawgListResponse<RawgGameResponse>>(url, ct);
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
        var semaphore = new SemaphoreSlim(2); // Зменшено з 4 до 2 для стабільності
        var result = new List<RawgGame>(basicList.Count);

        var tasks = basicList.Select(async basic =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                if (excludeIds?.Contains(basic.Id.ToString()) == true) return;

                var full = await GetGameDetailsAsync(basic.Id);
                
                // Throttling delay для уникнення rate limits
                await Task.Delay(100, ct);
                
                result.Add(full ?? basic);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return result;
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

            var response = await ExecuteRequestAsync<RawgListResponse<RawgGameResponse>>(url);
            
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