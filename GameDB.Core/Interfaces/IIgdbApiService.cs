namespace GameDB.Core.Interfaces;

public interface IIgdbApiService
{
    /// <summary>
    /// Fetches PC games from IGDB in batches of 500.
    /// Each call = 500 games with full metadata (genres, devs, pubs, store URLs).
    /// </summary>
    Task<List<IgdbGame>> GetPcGamesAsync(
        ISet<int>? excludeIgdbIds = null,
        CancellationToken ct = default);
}

public record IgdbGame
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Summary { get; init; }
    public long? FirstReleaseDate { get; init; } // Unix timestamp
    public List<string> Genres { get; init; } = new();
    public string? Developer { get; init; }
    public string? Publisher { get; init; }

    // Store URLs
    public string? SteamUrl { get; init; }    // category 13
    public string? GogUrl { get; init; }      // category 17
    public string? EgsUrl { get; init; }      // category 16
}
