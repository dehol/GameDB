using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class PriceHistory
{
    public int PriceHistoryId { get; set; }
    public int GameOfferId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public decimal Price { get; set; }
    public short DiscountPercent { get; set; }
    public string Currency { get; set; } = "USD";
    public GameOffer Offer { get; set; } = null!;
}