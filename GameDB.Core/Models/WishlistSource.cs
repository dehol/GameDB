namespace GameDB.Core.Models;

public class WishlistSource
{
    public int UserId { get; set; }
    public int GameId { get; set; }
    public int ShopId { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Game Game { get; set; } = null!;
    public GameShop Shop { get; set; } = null!;
}
