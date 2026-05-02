using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.Caching;

/// <summary>
/// In-memory cache for ITAD UUID lookups (ExternalId/Title -> ITAD UUID).
/// Reduces API calls by caching resolved UUIDs with a configurable TTL.
/// </summary>
public class ItadUuidCache
{
    private readonly TimeSpan _ttl;
    private readonly ILogger<ItadUuidCache> _logger;
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private DateTime _lastCleanup = DateTime.UtcNow;

    private const int DefaultTtlHours = 24;
    private const int CleanupIntervalMinutes = 30;

    public ItadUuidCache(ILogger<ItadUuidCache> logger)
    {
        _ttl = TimeSpan.FromHours(DefaultTtlHours);
        _logger = logger;
    }

    public bool TryGet(string key, out string uuid)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
            {
                uuid = entry.Uuid;
                return true;
            }

            uuid = string.Empty;
            return false;
        }
    }

    public void Set(string key, string uuid)
    {
        if (string.IsNullOrEmpty(uuid)) return;

        lock (_lock)
        {
            _cache[key] = new CacheEntry(uuid, DateTime.UtcNow + _ttl);
            MaybeCleanup();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            var count = _cache.Count;
            _cache.Clear();
            _logger.LogDebug("ITAD UUID cache cleared ({Count} entries removed)", count);
        }
    }

    private void MaybeCleanup()
    {
        if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(CleanupIntervalMinutes))
            return;

        _lastCleanup = DateTime.UtcNow;
        var expired = _cache.Count(kvp => kvp.Value.ExpiresAt <= DateTime.UtcNow);
        if (expired == 0) return;

        foreach (var key in _cache.Where(kvp => kvp.Value.ExpiresAt <= DateTime.UtcNow).Select(kvp => kvp.Key).ToList())
            _cache.Remove(key);

        _logger.LogDebug("ITAD UUID cache cleanup: removed {Count} expired entries", expired);
    }

    private record CacheEntry(string Uuid, DateTime ExpiresAt);
}
