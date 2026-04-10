using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class Publisher
{
    public int PublisherId { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<Game> Games { get; set; } = new List<Game>();
}