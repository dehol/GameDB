using System.Collections.Concurrent;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.Caching;

/// <summary>
/// Thread-safe cache for reference data (developers, publishers, genres)
/// </summary>
public class ReferenceDataCache
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReferenceDataCache> _logger;
    private readonly ConcurrentDictionary<string, int> _developers = new();
    private readonly ConcurrentDictionary<string, int> _publishers = new();
    private readonly ConcurrentDictionary<string, int> _genres = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ReferenceDataCache(AppDbContext db, ILogger<ReferenceDataCache> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<int?> GetOrCreateDeveloperIdAsync(string? name) =>
        GetOrCreateReferenceIdAsync(name, _developers, () => _db.Developers, "developer");

    public Task<int?> GetOrCreatePublisherIdAsync(string? name) =>
        GetOrCreateReferenceIdAsync(name, _publishers, () => _db.Publishers, "publisher");

    public async Task<int> GetOrCreateGenreIdAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Genre name cannot be null or empty", nameof(name));
        
        return await GetOrCreateReferenceIdAsync(name, _genres, () => _db.Genres, "genre") ?? 
            throw new InvalidOperationException($"Failed to create or find genre: {name}");
    }

    private async Task<int?> GetOrCreateReferenceIdAsync<TEntity>(
        string? name,
        ConcurrentDictionary<string, int> cache,
        Func<DbSet<TEntity>> dbSetGetter,
        string entityType) where TEntity : class, new()
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        // Fast path: check cache without lock
        if (cache.TryGetValue(name, out var cachedId))
            return cachedId;

        await _lock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (cache.TryGetValue(name, out cachedId))
                return cachedId;

            var dbSet = dbSetGetter();
            
            // Use reflection to find entity by Name property
            var existing = await dbSet
                .FirstOrDefaultAsync(e => EF.Property<string>(e, "Name") == name);

            if (existing != null)
            {
                var id = GetEntityId(existing, entityType);
                cache[name] = id;
                _logger.LogDebug("Cached existing {Type}: {Name} (ID: {Id})", entityType, name, id);
                return id;
            }

            // Create new entity using reflection
            var newEntity = new TEntity();
            typeof(TEntity).GetProperty("Name")?.SetValue(newEntity, name);
            
            dbSet.Add(newEntity);
            await _db.SaveChangesAsync();

            var newId = GetEntityId(newEntity, entityType);
            cache[name] = newId;
            _logger.LogInformation("Created new {Type}: {Name} (ID: {Id})", entityType, name, newId);
            return newId;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static int GetEntityId<TEntity>(TEntity entity, string entityType) where TEntity : class
    {
        var idPropertyName = $"{entityType}Id";
        var idProperty = typeof(TEntity).GetProperty(idPropertyName);
        if (idProperty?.GetValue(entity) is int id)
        {
            return id;
        }

        throw new InvalidOperationException(
            $"Unable to read primary key '{idPropertyName}' from {typeof(TEntity).Name}.");
    }

    public void Clear()
    {
        _developers.Clear();
        _publishers.Clear();
        _genres.Clear();
        _logger.LogInformation("Reference data cache cleared");
    }

    public async Task PreloadAsync()
    {
        _logger.LogInformation("Preloading reference data into cache...");

        var developers = await _db.Developers.ToListAsync();
        foreach (var dev in developers)
            _developers[dev.Name] = dev.DeveloperId;

        var publishers = await _db.Publishers.ToListAsync();
        foreach (var pub in publishers)
            _publishers[pub.Name] = pub.PublisherId;

        var genres = await _db.Genres.ToListAsync();
        foreach (var genre in genres)
            _genres[genre.Name] = genre.GenreId;

        _logger.LogInformation("Preloaded {Developers} developers, {Publishers} publishers, {Genres} genres",
            developers.Count, publishers.Count, genres.Count);
    }

    public CacheStatistics GetStatistics() =>
        new(_developers.Count, _publishers.Count, _genres.Count);
}

public record CacheStatistics(
    int DevelopersCount,
    int PublishersCount,
    int GenresCount
);
