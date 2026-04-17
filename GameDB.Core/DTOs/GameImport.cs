namespace GameDB.Core.DTOs;

/// <summary>
/// Unified game import model - supports Steam, GOG, EGS offers
/// </summary>
public record GameImport
{
    // Core game data
    public string Title { get; init; } = null!;
    public string NormalizedTitle { get; init; } = null!;
    public string? Description { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public string? Developer { get; init; }
    public string? Publisher { get; init; }
    public List<string> Genres { get; init; } = new();
    
    // Offers from different stores (0-N offers per game)
    public List<GameOfferImport> Offers { get; init; } = new();
    
    // External IDs for matching (RAWG ID used for deduplication)
    public int? RawgId { get; init; }
    
    // Rating from IGDB (0-100 scale)
    public double? Rating { get; init; }
    public int? RatingCount { get; init; }
    public string? CoverUrl { get; init; }
    public bool IsDlc { get; init; }
}

/// <summary>
/// Single offer from a store
/// </summary>
public record GameOfferImport
{
    public int ShopId { get; init; }
    public string ExternalId { get; init; } = null!;
    public decimal? CurrentPrice { get; init; }
    public short? CurrentDiscount { get; init; }
    public string Currency { get; init; } = "USD";
}

/// <summary>
/// Shop identifiers
/// </summary>
public static class ShopIds
{
    public const int Steam = 1;
    public const int Gog = 2;
    public const int EpicGames = 3;
}
