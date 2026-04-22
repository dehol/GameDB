namespace GameDB.Core.Models;

/// <summary>
/// Represents a single game item retrieved from an external platform wishlist.
/// </summary>
public class GameWishlistItem
{
    /// <summary>The unique identifier of the game on the external platform (e.g. Steam App ID).</summary>
    public string ExternalId { get; set; } = null!;

    /// <summary>The title of the game as returned by the external platform.</summary>
    public string Title { get; set; } = null!;

    /// <summary>Short description of the game provided by the external platform.</summary>
    public string? Description { get; set; }

    /// <summary>URL of the game's cover/header image on the external platform.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Current price of the game in the specified <see cref="Currency"/>.</summary>
    public decimal Price { get; set; }

    /// <summary>Current discount percentage (0–100).</summary>
    public int DiscountPercent { get; set; }

    /// <summary>ISO 4217 currency code for the price (e.g. "USD", "EUR").</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>The game's release date, if available.</summary>
    public DateTime? ReleaseDate { get; set; }
}
