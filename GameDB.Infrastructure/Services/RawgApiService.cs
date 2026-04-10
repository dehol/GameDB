using GameDB.Core.Configuration;
using GameDB.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GameDB.Infrastructure.Services;

public class RawgApiService : IRawgApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RawgApiService> _logger;
    private readonly RawgSettings _settings;

    public RawgApiService(
        HttpClient httpClient,
        ILogger<RawgApiService> logger,
        RawgSettings settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;
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
            _logger.LogWarning(ex, "Failed to get RAWG game details for ID {Id}", rawgId);
            return null;
        }
    }

    public async Task<List<RawgGame>> GetPopularGamesAsync(
        int limit = 1000,
        ISet<string>? excludeRawgIds = null,
        CancellationToken ct = default)
    {
        const string importDates = "2020-01-01,2026-12-31";
        const string ordering = "-rating";
        const int maxRawgPageSize = 40;

        _logger.LogInformation(
            "Fetching {Limit} popular RAWG games with dates={Dates}, ordering={Ordering}",
            limit, importDates, ordering);

        var games = new List<RawgGame>();
        var page = 1;
        var remaining = limit;
        var skippedCached = 0;

        while (remaining > 0)
        {
            var pageSize = Math.Min(remaining, maxRawgPageSize);
            var url = $"{_settings.ApiBaseUrl}/games?key={Uri.EscapeDataString(_settings.ApiKey)}" +
                      $"&dates={Uri.EscapeDataString(importDates)}" +
                      $"&ordering={Uri.EscapeDataString(ordering)}" +
                      $"&page_size={pageSize}&page={page}";

            var response = await ExecuteRequestAsync<RawgListResponse<RawgGameResponse>>(url, ct);

            if (response.Results.Count == 0)
                break;

            foreach (var game in response.Results.Select(MapToRawgGame))
            {
                if (excludeRawgIds != null && excludeRawgIds.Contains(game.Id.ToString()))
                {
                    skippedCached++;
                    continue;
                }

                games.Add(game);
                remaining--;
                if (remaining == 0)
                    break;
            }

            page++;

            if (response.Next == null)
                break;
        }

        _logger.LogInformation(
            "Fetched {Count} popular RAWG games ({SkippedCached} skipped from cache)",
            games.Count, skippedCached);
        return games;
    }

    private async Task<T> ExecuteRequestAsync<T>(string url, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(url, ct);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("RAWG API request failed: {StatusCode} - {Error}", 
                (int)response.StatusCode, errorContent);
            response.EnsureSuccessStatusCode();
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }) ?? throw new InvalidOperationException($"Failed to deserialize RAWG response from {url}");
    }

    private RawgGame MapToRawgGame(RawgGameResponse response)
    {
        // Extract Steam AppId from stores
        int? steamAppId = null;
        var steamStore = response.Stores?.FirstOrDefault(s => 
            s.Store?.Slug?.Equals("steam", StringComparison.OrdinalIgnoreCase) == true);
        
        if (steamStore?.Url != null)
        {
            // Extract AppId from Steam URL: https://store.steampowered.com/app/271590/
            var appIdMatch = System.Text.RegularExpressions.Regex.Match(
                steamStore.Url, @"/app/(\d+)");
            if (appIdMatch.Success && int.TryParse(appIdMatch.Groups[1].Value, out var appId))
            {
                steamAppId = appId;
            }
        }

        return new RawgGame
        {
            Id = response.Id,
            Name = response.Name,
            Description = response.DescriptionRaw ?? response.Description,
            Released = response.Released,
            BackgroundImage = response.BackgroundImage,
            Rating = response.Rating,
            Metacritic = response.Metacritic,
            Genres = response.Genres ?? new List<RawgGenre>(),
            Developers = response.Developers ?? new List<RawgDeveloper>(),
            Publishers = response.Publishers ?? new List<RawgPublisher>(),
            Stores = response.Stores ?? new List<RawgStore>(),
            Website = response.Website,
            SteamAppId = steamAppId
        };
    }

    // Response models for JSON deserialization
    private class RawgListResponse<T>
    {
        public int Count { get; set; }
        public string? Next { get; set; }
        public string? Previous { get; set; }
        public List<T> Results { get; set; } = new();
    }

    private class RawgGameResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? DescriptionRaw { get; set; }
        public string? Description { get; set; }
        public string? Released { get; set; }
        public string? BackgroundImage { get; set; }
        public double? Rating { get; set; }
        public int? Metacritic { get; set; }
        public List<RawgGenre> Genres { get; set; } = new();
        public List<RawgDeveloper> Developers { get; set; } = new();
        public List<RawgPublisher> Publishers { get; set; } = new();
        public List<RawgStore> Stores { get; set; } = new();
        public string? Website { get; set; }
    }
}
