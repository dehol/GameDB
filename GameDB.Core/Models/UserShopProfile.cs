using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class UserShopProfile
{
    [Key]
    public int ProfileId { get; set; }
    public int UserId { get; set; }
    public int ShopId { get; set; }
    public string ExternalUid { get; set; } = null!;
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public GameShop Shop { get; set; } = null!;
}