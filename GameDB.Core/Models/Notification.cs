using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class Notification
{
    [Key]
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public string Type { get; set; } = null!;
    public string? Payload { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}
