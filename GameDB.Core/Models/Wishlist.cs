using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class Wishlist
{
    public int UserId { get; set; }
    public int GameId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Game Game { get; set; } = null!;
    public ICollection<WishlistSource> Sources { get; set; } = new List<WishlistSource>();
}
