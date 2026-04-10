using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class Developer
{
    public int DeveloperId { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<Game> Games { get; set; } = new List<Game>();
}