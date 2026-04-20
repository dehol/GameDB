namespace GameDB.Core.DTOs;

/// <summary>
/// Row from vw_game_catalog view
/// </summary>
public record GameCatalogRow
{
    public int GameId { get; init; }
    public string Title { get; init; } = null!;
    public string? Description { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public string? developer_name { get; init; }
    public string? publisher_name { get; init; }
    public string? genres { get; init; }
    public decimal? min_price { get; init; }
    public short? max_discount { get; init; }
    public long? available_in_shops { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    // Rating from IGDB (0-100 scale)
    public double? rating { get; init; }
    public int? rating_count { get; init; }
    public bool is_dlc { get; init; }

    // Cover art source — populated from Game.CoverUrl + Steam fallback
    public string? cover_source { get; init; }
}

/// <summary>
/// Deal score from fn_get_deal_score function
/// </summary>
public record DealScoreRow
{
    public int listing_id { get; init; }
    public string shop_name { get; init; } = null!;
    public decimal current_price { get; init; }
    public short current_discount { get; init; }
    public decimal historical_low { get; init; }
    public decimal historical_avg { get; init; }
    public short deal_score { get; init; }
    public bool is_historical_low { get; init; }
}

/// <summary>
/// Result of a price sync operation
/// </summary>
public record SyncResult(int Scanned, int Updated, int Errors, List<SyncError>? ErrorDetails = null);

/// <summary>
/// Error during price sync
/// </summary>
public record SyncError(int GameOfferId, string? ExternalId, string ErrorMessage, int? StatusCode);

/// <summary>
/// User's wishlist item
/// </summary>
public record WishlistItemDto(int GameId, string GameTitle, DateTime AddedAt, string? SourceShop);

/// <summary>
/// User's alert from vw_user_alerts view
/// </summary>
public record UserAlertRow
{
    public int alert_id { get; init; }
    public string game_title { get; init; } = null!;
    public decimal? target_price { get; init; }
    public short? target_discount { get; init; }
    public decimal? best_current_price { get; init; }
    public short? best_current_discount { get; init; }
    public decimal? price_gap_pct { get; init; }
    public bool is_active { get; init; }
    public DateTime? triggered_at { get; init; }
    public DateTime created_at { get; init; }
}

/// <summary>
/// User's library from vw_user_library view
/// </summary>
public record UserLibraryRow
{
    public int UserId { get; init; }
    public int GameId { get; init; }
    public string game_title { get; init; } = null!;
    public DateOnly? ReleaseDate { get; init; }
    public string? developer_name { get; init; }
    public string shop_name { get; init; } = null!;
    public string? shop_url { get; init; }
    public string? DownloadUrl { get; init; }
    public decimal? purchase_store_price { get; init; }
    public DateTime AddedAt { get; init; }
}

public record NotificationItemDto(
    int NotificationId,
    string Type,
    string? Payload,
    bool IsRead,
    DateTime CreatedAt
);
