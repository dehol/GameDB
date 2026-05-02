using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace GameDB.Infrastructure.Services;

public record ItadPrice(decimal Amount, string Currency, int Cut);

public interface IItadClient
{
    Task<Dictionary<string, string>> LookupIdsAsync(int shopId, IEnumerable<string> externalIds, CancellationToken ct);
    Task<Dictionary<string, string>> LookupUuidsByTitleAsync(IEnumerable<string> titles, CancellationToken ct);
    Task<Dictionary<string, ItadPrice>> GetPricesAsync(int shopId, IEnumerable<string> itadUuids, CancellationToken ct);
}

internal sealed class ItadRateLimiter
{
    private readonly SemaphoreSlim _semaphore;
    private readonly TimeSpan _minDelay;
    private DateTime _lastRequestUtc = DateTime.MinValue;
    private readonly object _lock = new();

    public ItadRateLimiter(int maxConcurrency = 2, int minDelayMs = 500)
    {
        _semaphore = new SemaphoreSlim(maxConcurrency);
        _minDelay = TimeSpan.FromMilliseconds(minDelayMs);
    }

    public async Task WaitAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        _semaphore.Release();

        TimeSpan delay;
        lock (_lock)
        {
            var elapsed = DateTime.UtcNow - _lastRequestUtc;
            if (elapsed < _minDelay)
            {
                delay = _minDelay - elapsed;
                _lastRequestUtc = DateTime.UtcNow + delay;
            }
            else
            {
                delay = TimeSpan.Zero;
                _lastRequestUtc = DateTime.UtcNow;
            }
        }

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, ct);
        }
    }
}

public sealed class ItadClient : IItadClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ItadClient> _logger;
    private readonly ItadRateLimiter _limiter;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.isthereanydeal.com";

    public ItadClient(IHttpClientFactory factory, ILogger<ItadClient> logger, ItadSettings settings)
    {
        _http = factory.CreateClient();
        // Встановлюємо короткий таймаут: якщо ITAD завис, краще швидко впасти і спробувати ще раз
        _http.Timeout = TimeSpan.FromSeconds(15); 
        _logger = logger;
        _apiKey = settings.ApiKey;
        _limiter = new ItadRateLimiter();
    }

    public async Task<Dictionary<string, string>> LookupIdsAsync(int shopId, IEnumerable<string> externalIds, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in externalIds.Chunk(150))
        {
            int retries = 3;
            while (retries > 0)
            {
                try 
                {
                    await _limiter.WaitAsync(ct);

                    // ВИПРАВЛЕНО: Прибрано /games/ з URL
                    var url = $"{BaseUrl}/lookup/id/shop/{shopId}/v1?key={_apiKey}";
                    
                    string body;
                    if (shopId == 61) // Steam
                    {
                        var intIds = chunk.Where(id => int.TryParse(id, out _)).Select(int.Parse).ToArray();
                        body = JsonSerializer.Serialize(intIds);
                    }
                    else // GOG / Інші
                    {
                        body = JsonSerializer.Serialize(chunk.ToArray());
                    }

                    var resp = await _http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"), ct);

                    if (resp.IsSuccessStatusCode)
                    {
                        var json = await resp.Content.ReadAsStringAsync(ct);

                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (root.ValueKind == JsonValueKind.Object)
                        {
                            // Dictionary format: {"730": "uuid", "570": null}
                            foreach (var prop in root.EnumerateObject())
                            {
                                var val = prop.Value.ValueKind == JsonValueKind.String
                                    ? prop.Value.GetString()
                                    : null;
                                if (!string.IsNullOrEmpty(val))
                                    result[prop.Name] = val;
                            }
                        }
                        else if (root.ValueKind == JsonValueKind.Array)
                        {
                            // Array format: [{"id":"730","game":{"id":"uuid"}}, ...]
                            foreach (var item in root.EnumerateArray())
                            {
                                var extId = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

                                // game_id (flat) or game.id (nested)
                                string? gameId = null;
                                if (item.TryGetProperty("game_id", out var gidEl) && gidEl.ValueKind == JsonValueKind.String)
                                    gameId = gidEl.GetString();
                                else if (item.TryGetProperty("game", out var gameEl)
                                      && gameEl.ValueKind == JsonValueKind.Object
                                      && gameEl.TryGetProperty("id", out var nestedId)
                                      && nestedId.ValueKind == JsonValueKind.String)
                                    gameId = nestedId.GetString();

                                if (!string.IsNullOrEmpty(extId) && !string.IsNullOrEmpty(gameId))
                                    result[extId] = gameId;
                            }
                        }

                        if (result.Count == 0)
                        {
                            _logger.LogWarning("ITAD lookup returned 0 results. First 500 chars: {Json}",
                                json.Length > 500 ? json[..500] + "..." : json);
                        }
                        break; // Успіх
                    }

                    if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        await HandleRateLimit(resp, ct);
                        retries--;
                        continue;
                    }
                    
                    _logger.LogWarning("ITAD lookup failed with status {Status}", resp.StatusCode);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("ITAD lookup chunk exception (Retries left: {Retries}): {Msg}", retries - 1, ex.Message);
                    retries--;
                    await Task.Delay(1000, ct);
                }
            }
        }
        return result;
    }

    public async Task<Dictionary<string, string>> LookupUuidsByTitleAsync(IEnumerable<string> titles, CancellationToken ct)
    {
        var result = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in titles.Chunk(12))
        {
            var tasks = chunk.Select(async title =>
            {
                int retries = 3;
                while (retries > 0)
                {
                    try
                    {
                        await _limiter.WaitAsync(ct);

                        var url = $"{BaseUrl}/games/lookup/v1?key={_apiKey}&title={Uri.EscapeDataString(title)}";
                        var resp = await _http.GetAsync(url, ct);

                        if (resp.IsSuccessStatusCode)
                        {
                            var json = await resp.Content.ReadAsStringAsync(ct);
                            using var doc = JsonDocument.Parse(json);
                            
                            if (doc.RootElement.TryGetProperty("game", out var game) && game.ValueKind != JsonValueKind.Null && game.TryGetProperty("id", out var idEl))
                            {
                                var uuid = idEl.GetString();
                                if (!string.IsNullOrEmpty(uuid)) result[title] = uuid;
                            }
                            break;
                        }

                        if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            await HandleRateLimit(resp, ct);
                            retries--;
                            continue;
                        }
                        break;
                    }
                    catch (Exception ex) // Захист від TaskCanceledException (таймаутів)
                    {
                        _logger.LogWarning("ITAD title lookup timeout/error for '{Title}'. Retries left: {Retries}", title, retries - 1);
                        retries--;
                        await Task.Delay(1000, ct);
                    }
                }
            });

            await Task.WhenAll(tasks);
        }

        return new Dictionary<string, string>(result, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, ItadPrice>> GetPricesAsync(int shopId, IEnumerable<string> itadUuids, CancellationToken ct)
    {
        var result = new Dictionary<string, ItadPrice>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in itadUuids.Chunk(200))
        {
            int retries = 3;
            while (retries > 0)
            {
                try
                {
                    await _limiter.WaitAsync(ct);

                    var url = $"{BaseUrl}/games/prices/v3?key={_apiKey}&shops={shopId}&country=US";
                    var body = JsonSerializer.Serialize(chunk.ToArray());

                    var resp = await _http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"), ct);

                    if (resp.IsSuccessStatusCode)
                    {
                        var json = await resp.Content.ReadAsStringAsync(ct);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        
                        JsonElement items = default;
                        if (root.ValueKind == JsonValueKind.Array) items = root;
                        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("prices", out var p)) items = p;
                        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d)) items = d;

                        if (items.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var game in items.EnumerateArray())
                            {
                                if (!game.TryGetProperty("id", out var idEl)) continue;
                                var id = idEl.GetString();
                                if (string.IsNullOrEmpty(id)) continue;
                                if (!game.TryGetProperty("deals", out var deals)) continue;

                                foreach (var deal in deals.EnumerateArray())
                                {
                                    if (!deal.TryGetProperty("shop", out var shop) || !shop.TryGetProperty("id", out var shopIdEl) || shopIdEl.GetInt32() != shopId) continue;
                                    if (!deal.TryGetProperty("price", out var priceEl)) continue;

                                    var amount = priceEl.TryGetProperty("amount", out var a) ? a.GetDecimal() : 0m;
                                    var currency = priceEl.TryGetProperty("currency", out var c) ? c.GetString() ?? "USD" : "USD";
                                    var cut = deal.TryGetProperty("cut", out var cutEl) ? cutEl.GetInt32() : 0;

                                    result[id] = new ItadPrice(amount, currency, cut);
                                    break;
                                }
                            }
                        }
                        break;
                    }

                    if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        await HandleRateLimit(resp, ct);
                        retries--;
                        continue;
                    }
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("ITAD prices chunk exception (Retries left: {Retries}): {Msg}", retries - 1, ex.Message);
                    retries--;
                    await Task.Delay(1000, ct);
                }
            }
        }

        return result;
    }

    private async Task HandleRateLimit(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.Headers.TryGetValues("Retry-After", out var v) && int.TryParse(v.FirstOrDefault(), out var sec))
        {
            _logger.LogWarning("ITAD rate limit hit → retry after {Sec}s", sec);
            await Task.Delay(TimeSpan.FromSeconds(sec), ct);
        }
        else
        {
            _logger.LogWarning("ITAD rate limit hit → default 5s delay");
            await Task.Delay(5000, ct);
        }
    }
}