using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class GameShop
{
    [Key]
    public int ShopId { get; set; }
    public string Name { get; set; } = null!;
    public string? BaseUrl { get; set; }
    public string? ApiBaseUrl { get; set; }
    public ICollection<GameOffer> Offers { get; set; } = new List<GameOffer>();
    public ICollection<UserShopProfile> UserProfiles { get; set; } = new List<UserShopProfile>();
    public ICollection<UserLibrary> LibraryEntries { get; set; } = new List<UserLibrary>();
}