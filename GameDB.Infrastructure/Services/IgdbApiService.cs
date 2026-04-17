using GameDB.Core.Configuration;
using GameDB.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameDB.Infrastructure.Services;

/// <summary>
/// IGDB API client.
/// - Auth via Twitch client_credentials (token auto-refreshed)
/// - Batch queries: 500 games per request with full metadata
/// - PC platform (id=6), includes base games and DLC/expansions
/// - Filters games that have at least one of: Steam, GOG, EGS store URL
/// </summary>
public class IgdbApiService : IIgdbApiService
{
    private readonly HttpClient _http;
    private readonly ILogger<IgdbApiService> _logger;
    private readonly IgdbSettings _settings;

    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    // IGDB website categories
    private const int SteamCategory = 13;
    private const int GogCategory   = 17;
    private const int EgsCategory   = 16;

    // IGDB PC platform ID
    private const int PcPlatformId = 6;

    // Max games per IGDB request
    private const int BatchSize = 500;

    // Query profiles from strict to permissive for resilience
    private static readonly string StrictPcContentWhereClause = $"platforms = ({PcPlatformId}) & category = (0,1,2,4) & websites != null";
    private static readonly string PcWithWebsitesWhereClause = $"platforms = ({PcPlatformId}) & websites != null";
    private const string MainWithWebsitesWhereClause = "category = 0 & websites != null";
    private const string WebsitesOnlyWhereClause = "websites != null";

    // Rate limit: 4 req/sec
    private readonly SemaphoreSlim _rateLimiter = new(4, 4);

    public IgdbApiService(HttpClient http, ILogger<IgdbApiService> logger, IgdbSettings settings)
    {
        _http = http;
        _logger = logger;
        _settings = settings;
    }

    public async Task<List<IgdbGame>> GetPcGamesAsync(
        ISet<int>? excludeIgdbIds = null,
        ISet<int>? includeIgdbIds = null,
        int? maxGames = null,
        CancellationToken ct = default)
    {
        await EnsureTokenAsync(ct);

        var result = new List<IgdbGame>();
        var offset = 0;
        var total = int.MaxValue;
        var whereClause = StrictPcContentWhereClause;

        _logger.LogInformation("📥 Fetching PC games from IGDB...");

        while (offset < total)
        {
            var query = BuildQuery(offset, whereClause);
            var batch = await PostQueryAsync<List<IgdbRawGame>>("games", query, ct);

            if ((batch == null || batch.Count == 0) && offset == 0)
            {
                var fallbackClauses = new[]
                {
                    PcWithWebsitesWhereClause,
                    MainWithWebsitesWhereClause,
                    WebsitesOnlyWhereClause
                };

                foreach (var fallbackClause in fallbackClauses)
                {
                    _logger.LogWarning(
                        "IGDB returned 0 results for query profile '{Profile}', trying fallback profile '{FallbackProfile}'",
                        whereClause, fallbackClause);

                    whereClause = fallbackClause;
                    query = BuildQuery(offset, whereClause);
                    batch = await PostQueryAsync<List<IgdbRawGame>>("games", query, ct);

                    if (batch is { Count: > 0 })
                    {
                        _logger.LogInformation("IGDB fallback profile '{Profile}' returned {Count} games",
                            whereClause, batch.Count);
                        break;
                    }
                }
            }

            if (batch == null || batch.Count == 0) break;

            // First request — log approximate total
            if (offset == 0)
                _logger.LogInformation("📥 IGDB: fetching games (batch size {Size}, profile {Profile})", BatchSize, whereClause);

            foreach (var raw in batch)
            {
                if (excludeIgdbIds?.Contains(raw.Id) == true) continue;
                if (includeIgdbIds != null && !includeIgdbIds.Contains(raw.Id)) continue;

                var game = MapGame(raw);
                // Only include games available on at least one store
                if (game.SteamUrl != null || game.GogUrl != null || game.EgsUrl != null)
                {
                    result.Add(game);
                    if (maxGames.HasValue && result.Count >= maxGames.Value)
                        break;
                }
            }

            if (maxGames.HasValue && result.Count >= maxGames.Value)
                break;

            offset += batch.Count;

            if (batch.Count < BatchSize) break; // last page

            // Respect rate limit between pages
            await Task.Delay(300, ct);
        }

        _logger.LogInformation("📥 IGDB: {Count} PC games with store links fetched", result.Count);
        return result;
    }

    // ── Query builder ─────────────────────────────────────────────────────

    private static string BuildQuery(int offset, string whereClause) => $"""
        fields name, summary, first_release_date,
               rating, rating_count,
               category,
               genres.name,
               involved_companies.company.name,
               involved_companies.developer,
               involved_companies.publisher,
               websites.url, websites.category;
        where {whereClause};
        limit {BatchSize};
        offset {offset};
        """;

    // ── HTTP ──────────────────────────────────────────────────────────────

    private async Task<T?> PostQueryAsync<T>(string endpoint, string query, CancellationToken ct)
    {
        await _rateLimiter.WaitAsync(ct);
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"{_settings.ApiBaseUrl}/{endpoint}")
            {
                Content = new StringContent(query, Encoding.UTF8, "text/plain"),
                Headers =
                {
                    { "Client-ID", _settings.ClientId },
                    { "Authorization", $"Bearer {_accessToken}" }
                }
            };

            using var resp = await _http.SendAsync(request, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("IGDB {Endpoint} failed {Status}: {Body}",
                    endpoint, resp.StatusCode, body);
                return default;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<T>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        finally
        {
            // Release after 250ms to stay under 4 req/sec
            _ = Task.Delay(250, ct).ContinueWith(_ => _rateLimiter.Release());
        }
    }

    // ── Token management ─────────────────────────────────────────────────

    private async Task EnsureTokenAsync(CancellationToken ct)
    {
        if (_accessToken != null && DateTime.UtcNow < _tokenExpiry) return;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_accessToken != null && DateTime.UtcNow < _tokenExpiry) return;

            _logger.LogInformation("🔑 Refreshing IGDB access token...");

            var url = $"{_settings.TokenUrl}" +
                      $"?client_id={_settings.ClientId}" +
                      $"&client_secret={_settings.ClientSecret}" +
                      $"&grant_type=client_credentials";

            using var resp = await _http.PostAsync(url, null, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            var token = JsonSerializer.Deserialize<TwitchTokenResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Failed to parse Twitch token");

            _accessToken = token.AccessToken;
            // Refresh 1 hour before expiry
            _tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn - 3600);

            _logger.LogInformation("🔑 IGDB token refreshed, expires in {Days} days",
                token.ExpiresIn / 86400);
        }
        finally { _tokenLock.Release(); }
    }

    // ── Mapping ───────────────────────────────────────────────────────────

    private static IgdbGame MapGame(IgdbRawGame raw)
    {
        var developer  = raw.InvolvedCompanies?
            .FirstOrDefault(c => c.Developer)?.Company?.Name;
        var publisher  = raw.InvolvedCompanies?
            .FirstOrDefault(c => c.Publisher)?.Company?.Name
            ?? developer; // fallback

        var steamUrl = raw.Websites?
            .FirstOrDefault(w => w.Category == SteamCategory || IsSteamUrl(w.Url))?.Url;
        var gogUrl   = raw.Websites?
            .FirstOrDefault(w => w.Category == GogCategory || IsGogUrl(w.Url))?.Url;
        var egsUrl   = raw.Websites?
            .FirstOrDefault(w => w.Category == EgsCategory || IsEgsUrl(w.Url))?.Url;

        return new IgdbGame
        {
            Id                = raw.Id,
            Name              = raw.Name,
            Summary           = raw.Summary,
            FirstReleaseDate  = raw.FirstReleaseDate,
            Genres            = raw.Genres?.Select(g => g.Name).ToList() ?? new(),
            Developer         = developer,
            Publisher         = publisher,
            SteamUrl          = steamUrl,
            GogUrl            = gogUrl,
            EgsUrl            = egsUrl,
            Rating            = raw.Rating,
            RatingCount       = raw.RatingCount,
            IsDlc             = raw.Category is 1 or 2 or 4,
        };
    }

    // ── Raw JSON models ───────────────────────────────────────────────────

    private record IgdbRawGame(
        int Id,
        string Name,
        string? Summary,
        [property: JsonPropertyName("first_release_date")] long? FirstReleaseDate,
        double? Rating,
        [property: JsonPropertyName("rating_count")] int? RatingCount,
        int? Category,
        List<IgdbGenre>? Genres,
        [property: JsonPropertyName("involved_companies")] List<IgdbInvolvedCompany>? InvolvedCompanies,
        List<IgdbWebsite>? Websites);

    private record IgdbGenre(
        [property: JsonPropertyName("id")] int Id, 
        [property: JsonPropertyName("name")] string Name);

    private record IgdbInvolvedCompany(
        bool Developer,
        bool Publisher,
        IgdbCompany? Company);

    private record IgdbCompany(int Id, string Name);

    private record IgdbWebsite(int Id, int Category, string Url);

    private static bool IsSteamUrl(string? url) =>
        HasExpectedHost(url, "store.steampowered.com");

    private static bool IsGogUrl(string? url) =>
        HasExpectedHost(url, "gog.com");

    private static bool IsEgsUrl(string? url) =>
        HasExpectedHost(url, "epicgames.com") ||
        HasExpectedHost(url, "epic.games");

    private static bool HasExpectedHost(string? url, string expectedHost)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Host, expectedHost, StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith($".{expectedHost}", StringComparison.OrdinalIgnoreCase);
    }

    private record TwitchTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")]   int ExpiresIn);
}
