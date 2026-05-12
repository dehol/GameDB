using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class ImportJobLog
{
    [Key]
    public int ImportJobLogId { get; set; }
    public int ImportJobId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ImportJobLogLevel Level { get; set; } = ImportJobLogLevel.Info;
    public string Phase { get; set; } = "general";
    public string Message { get; set; } = null!;
    public string? Data { get; set; }

    public ImportJob ImportJob { get; set; } = null!;
}
