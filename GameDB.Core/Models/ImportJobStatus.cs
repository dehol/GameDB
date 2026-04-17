namespace GameDB.Core.Models;

/// <summary>
/// Status of an import job
/// </summary>
public enum ImportJobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
    CompletedWithWarnings = 5
}
