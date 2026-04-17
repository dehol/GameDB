namespace GameDB.Core.DTOs;

/// <summary>
/// DTO for game details response (matches frontend expectations)
/// </summary>
public class GameDetailsDto
{
    public int GameId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Rating from IGDB
    public double? Rating { get; set; }
    public int? RatingCount { get; set; }
    public string? CoverUrl { get; set; }
    
    public DeveloperDto? Developer { get; set; }
    public PublisherDto? Publisher { get; set; }
    public List<string> Genres { get; set; } = new();
    public List<GameOfferDto> Offers { get; set; } = new();
}

public class DeveloperDto
{
    public int DeveloperId { get; set; }
    public string Name { get; set; } = null!;
}

public class PublisherDto
{
    public int PublisherId { get; set; }
    public string Name { get; set; } = null!;
}

public class GameOfferDto
{
    public int GameOfferId { get; set; }
    public int ShopId { get; set; }
    public string ShopName { get; set; } = null!;
    public string? ExternalId { get; set; }
    public string? DownloadUrl { get; set; }
    public decimal CurrentPrice { get; set; }
    public short CurrentDiscount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? PriceSyncedAt { get; set; }
    public List<PriceHistoryDto> PriceHistory { get; set; } = new();
}

public class PriceHistoryDto
{
    public int PriceHistoryId { get; set; }
    public decimal Price { get; set; }
    public short DiscountPercent { get; set; }
    public DateTime RecordedAt { get; set; }
}
