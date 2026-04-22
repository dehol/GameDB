using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

/// <summary>
/// Represents a wishlist import operation from an external store (Steam, GOG, EGS).
/// </summary>
public class WishlistImport
{
    [Key]
    public int ImportId { get; set; }

    public int UserId { get; set; }

    public int ShopId { get; set; }

    /// <summary>
    /// Current status: InProgress, Completed, Failed, Cancelled
    /// </summary>
    public string Status { get; set; } = "InProgress";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Total number of items found in the external wishlist.
    /// </summary>
    public int ItemsCount { get; set; }

    /// <summary>
    /// Number of items successfully imported.
    /// </summary>
    public int ImportedCount { get; set; }

    /// <summary>
    /// Number of items skipped (already in wishlist).
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// Number of items that failed to import.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Error message if the entire import failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    public User User { get; set; } = null!;
    public GameShop Shop { get; set; } = null!;
}
