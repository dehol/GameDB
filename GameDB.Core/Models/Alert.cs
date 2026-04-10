using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class Alert
{
    public int AlertId { get; set; }
    public int UserId { get; set; }
    public int GameId { get; set; }
    public decimal? TargetPrice { get; set; }
    public short? TargetDiscount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TriggeredAt { get; set; }
    public DateTime? LastNotifiedAt { get; set; }
    public User User { get; set; } = null!;
    public Game Game { get; set; } = null!;
}
