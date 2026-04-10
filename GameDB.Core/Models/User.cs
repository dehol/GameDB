using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class User
{
    [Key]
    public int UserId { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin { get; set; }
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
    public ICollection<UserShopProfile> ShopProfiles { get; set; } = new List<UserShopProfile>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    public ICollection<UserLibrary> Library { get; set; } = new List<UserLibrary>();
}