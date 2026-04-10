using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class Game
{
    public int GameId { get; set; }
    public string Title { get; set; } = null!;
    public string? NormalizedTitle { get; set; }
    public string? Description { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public int? DeveloperId { get; set; }
    public int? PublisherId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Developer? Developer { get; set; }
    public Publisher? Publisher { get; set; }
    public ICollection<GameGenre> GameGenres { get; set; } = new List<GameGenre>();
    public ICollection<GameOffer> Offers { get; set; } = new List<GameOffer>();
    public ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    public ICollection<UserLibrary> LibraryEntries { get; set; } = new List<UserLibrary>();
}