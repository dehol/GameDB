using System.Collections.Concurrent;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.Caching;

/// <summary>
/// Thread-safe cache for reference data (developers, publishers, genres)
/// Without reflection - explicit methods for each entity type
/// </summary>
public class ReferenceDataCache
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReferenceDataCache> _logger;
    private readonly ConcurrentDictionary<string, int> _developers = new();
    private readonly ConcurrentDictionary<string, int> _publishers = new();
    private readonly ConcurrentDictionary<string, int> _genres = new();
    private readonly SemaphoreSlim _developerLock = new(1, 1);
    private readonly SemaphoreSlim _publisherLock = new(1, 1);
    private readonly SemaphoreSlim _genreLock = new(1, 1);

    public ReferenceDataCache(AppDbContext db, ILogger<ReferenceDataCache> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int?> GetOrCreateDeveloperIdAsync(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        // Fast path: check cache without lock
        if (_developers.TryGetValue(name, out var cachedId))
            return cachedId;

        await _developerLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_developers.TryGetValue(name, out cachedId))
                return cachedId;

            var existing = await _db.Developers
                .FirstOrDefaultAsync(d => d.Name == name);

            if (existing != null)
            {
                _developers[name] = existing.DeveloperId;
                _logger.LogDebug("Cached existing developer: {Name} (ID: {Id})", name, existing.DeveloperId);
                return existing.DeveloperId;
            }

            var newDeveloper = new Developer { Name = name };
            _db.Developers.Add(newDeveloper);
            await _db.SaveChangesAsync();

            _developers[name] = newDeveloper.DeveloperId;
            _logger.LogInformation("Created new developer: {Name} (ID: {Id})", name, newDeveloper.DeveloperId);
            return newDeveloper.DeveloperId;
        }
        finally
        {
            _developerLock.Release();
        }
    }

    public async Task<int?> GetOrCreatePublisherIdAsync(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        // Fast path: check cache without lock
        if (_publishers.TryGetValue(name, out var cachedId))
            return cachedId;

        await _publisherLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_publishers.TryGetValue(name, out cachedId))
                return cachedId;

            var existing = await _db.Publishers
                .FirstOrDefaultAsync(p => p.Name == name);

            if (existing != null)
            {
                _publishers[name] = existing.PublisherId;
                _logger.LogDebug("Cached existing publisher: {Name} (ID: {Id})", name, existing.PublisherId);
                return existing.PublisherId;
            }

            var newPublisher = new Publisher { Name = name };
            _db.Publishers.Add(newPublisher);
            await _db.SaveChangesAsync();

            _publishers[name] = newPublisher.PublisherId;
            _logger.LogInformation("Created new publisher: {Name} (ID: {Id})", name, newPublisher.PublisherId);
            return newPublisher.PublisherId;
        }
        finally
        {
            _publisherLock.Release();
        }
    }

    public async Task<int> GetOrCreateGenreIdAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Genre name cannot be null or empty", nameof(name));

        // Fast path: check cache without lock
        if (_genres.TryGetValue(name, out var cachedId))
            return cachedId;

        await _genreLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_genres.TryGetValue(name, out cachedId))
                return cachedId;

            var existing = await _db.Genres
                .FirstOrDefaultAsync(g => g.Name == name);

            if (existing != null)
            {
                _genres[name] = existing.GenreId;
                _logger.LogDebug("Cached existing genre: {Name} (ID: {Id})", name, existing.GenreId);
                return existing.GenreId;
            }

            var newGenre = new Genre { Name = name };
            _db.Genres.Add(newGenre);
            await _db.SaveChangesAsync();

            _genres[name] = newGenre.GenreId;
            _logger.LogInformation("Created new genre: {Name} (ID: {Id})", name, newGenre.GenreId);
            return newGenre.GenreId;
        }
        finally
        {
            _genreLock.Release();
        }
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
