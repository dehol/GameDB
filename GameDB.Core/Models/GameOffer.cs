using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class GameOffer
{
    public int GameOfferId { get; set; }
    public int GameId { get; set; }
    public int ShopId { get; set; }
    public string? ExternalId { get; set; }
    public string? DownloadUrl { get; set; }
    public decimal CurrentPrice { get; set; }
    public short CurrentDiscount { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsFree { get; set; }
    public DateTime? PriceSyncedAt { get; set; }
    public Game Game { get; set; } = null!;
    public GameShop Shop { get; set; } = null!;
    public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
}