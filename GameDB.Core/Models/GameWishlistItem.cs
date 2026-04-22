namespace GameDB.Core.Models;

/// <summary>
/// Represents a game item retrieved from an external store's wishlist API.
/// </summary>
public class GameWishlistItem
{
    /// <summary>
    /// External game identifier on the store platform.
    /// </summary>
    public string ExternalId { get; set; } = null!;

    /// <summary>
    /// Game title as returned by the store.
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Game description as returned by the store.
    /// </summary>
    public string? Description { get; set; }
}
