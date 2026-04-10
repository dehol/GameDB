namespace GameDB.Core.Interfaces;

public interface IRawgApiService
{
    Task<List<RawgGame>> SearchGamesAsync(string query, int limit = 500);
    Task<RawgGame?> GetGameDetailsAsync(int rawgId);
    Task<List<RawgGame>> GetPopularGamesAsync(
        int limit = 1000,
        ISet<string>? excludeRawgIds = null,
        CancellationToken ct = default);
}

public record RawgGame
{
    public int Id { get; init; } // RAWG ID
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public string? Released { get; init; } // Date as string (YYYY-MM-DD)
    public string? BackgroundImage { get; init; }
    public double? Rating { get; init; }
    public int? Metacritic { get; init; }
    public List<RawgGenre> Genres { get; init; } = new();
    public List<RawgDeveloper> Developers { get; init; } = new();
    public List<RawgPublisher> Publishers { get; init; } = new();
    public List<RawgStore> Stores { get; init; } = new();
    public string? Website { get; init; }
    
    // Extracted Steam AppId (if available)
    public int? SteamAppId { get; init; }
}

public record RawgGenre
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Slug { get; init; }
}

public record RawgDeveloper
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Slug { get; init; }
}

public record RawgPublisher
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Slug { get; init; }
}

public record RawgStore
{
    public int Id { get; init; }
    public string? Url { get; init; }
    public RawgStoreInfo? Store { get; init; }
}

public record RawgStoreInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Slug { get; init; }
}

// RAWG API Response models
internal record RawgListResponse<T>
{
    public int Count { get; init; }
    public string? Next { get; init; }
    public string? Previous { get; init; }
    public List<T> Results { get; init; } = new();
}

internal record RawgGameResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string? DescriptionRaw { get; init; }
    public string? Description { get; init; }
    public string? Released { get; init; }
    public string? BackgroundImage { get; init; }
    public double? Rating { get; init; }
    public int? Metacritic { get; init; }
    public List<RawgGenre> Genres { get; init; } = new();
    public List<RawgDeveloper> Developers { get; init; } = new();
    public List<RawgPublisher> Publishers { get; init; } = new();
    public List<RawgStore> Stores { get; init; } = new();
    public string? Website { get; init; }
}
