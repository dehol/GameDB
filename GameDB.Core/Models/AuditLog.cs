using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class AuditLog
{
    [Key]
    public int AuditLogId { get; set; }
    public int? UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ActionType { get; set; } = null!;
    public string? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IPAddress { get; set; }
}
