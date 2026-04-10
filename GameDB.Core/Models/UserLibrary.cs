using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class UserLibrary
{
    public int UserId { get; set; }
    public int GameId { get; set; }
    public int ShopId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Game Game { get; set; } = null!;
    public GameShop Shop { get; set; } = null!;
}
