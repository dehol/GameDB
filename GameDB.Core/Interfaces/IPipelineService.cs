using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

public interface IPipelineService
{
    /// <summary>
    /// Starts a new import pipeline. Only one pipeline can run at a time.
    /// </summary>
    /// <returns>Pipeline ID (same as ImportJob ID)</returns>
    /// <exception cref="InvalidOperationException">Thrown when a pipeline is already running</exception>
    Task<int> StartImportPipelineAsync(ImportPipelineOptions? options = null);

    /// <summary>
    /// Gets the current status of a pipeline
    /// </summary>
    Task<PipelineStatus?> GetPipelineStatusAsync(int pipelineId);

    /// <summary>
    /// Requests cancellation of a running pipeline
    /// </summary>
    Task CancelPipelineAsync(int pipelineId);

    /// <summary>
    /// Checks if any pipeline is currently running
    /// </summary>
    Task<bool> IsPipelineRunningAsync();

    /// <summary>
    /// Gets the currently running pipeline ID, if any
    /// </summary>
    Task<int?> GetRunningPipelineIdAsync();
}

public record ImportPipelineOptions(
    int? Limit = null,
    List<int>? IgdbGameIds = null,
    List<int>? GameIds = null,
    bool OverwriteExisting = false);

public record PipelineStatus(
    int PipelineId,
    string Status, // "pending", "running", "completed", "failed", "cancelled"
    string? Phase, // "collecting_igdb", "collecting_steam", "staging", "importing", etc.
    int TotalGames,
    int ProcessedGames,
    int ImportedGames,
    int ErrorCount,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage
)
{
    public double ProgressPercent => TotalGames > 0 
        ? Math.Round(100.0 * ProcessedGames / TotalGames, 2) 
        : 0;
    
    public TimeSpan? Duration => CompletedAt?.Subtract(StartedAt) ?? DateTime.UtcNow.Subtract(StartedAt);
}
