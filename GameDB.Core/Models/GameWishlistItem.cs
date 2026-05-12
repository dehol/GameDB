namespace GameDB.Core.Models;

public class GameWishlistItem
{
    public string ExternalId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public int DiscountPercent { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? ReleaseDate { get; set; }
}
