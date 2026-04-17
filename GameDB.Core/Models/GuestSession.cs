using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class GuestSession
{
    [Key]
    public int GuestSessionId { get; set; }
    public int UserId { get; set; }
    public string DeviceHash { get; set; } = null!;
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public User User { get; set; } = null!;
}
