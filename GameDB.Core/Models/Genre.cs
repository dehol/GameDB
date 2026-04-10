using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class Genre
{
    public int GenreId { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<GameGenre> GameGenres { get; set; } = new List<GameGenre>();
}