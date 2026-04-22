using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

/// <summary>
/// Tracks the state of a user's wishlist import operation from an external platform.
/// </summary>
public class WishlistImport
{
    /// <summary>Primary key.</summary>
    [Key]
    public int ImportId { get; set; }

    /// <summary>ID of the user who initiated the import.</summary>
    public int UserId { get; set; }

    /// <summary>ID of the external shop (references <see cref="GameShop"/>).</summary>
    public int ShopId { get; set; }

    /// <summary>
    /// Current status of the import.
    /// Possible values: <c>Pending</c>, <c>InProgress</c>, <c>Completed</c>, <c>Failed</c>.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Total number of items found in the external wishlist.</summary>
    public int ItemsCount { get; set; }

    /// <summary>Number of items successfully imported so far.</summary>
    public int ImportedCount { get; set; }

    /// <summary>Number of items that failed to import.</summary>
    public int ErrorCount { get; set; }

    /// <summary>Optional error message if the import encountered a terminal failure.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>UTC timestamp when the import was started.</summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the import finished (either successfully or with failure).</summary>
    public DateTime? CompletedAt { get; set; }

    // Navigation properties

    /// <summary>The user who initiated this import.</summary>
    public virtual User User { get; set; } = null!;

    /// <summary>The external shop this import originates from.</summary>
    public virtual GameShop Shop { get; set; } = null!;
}
