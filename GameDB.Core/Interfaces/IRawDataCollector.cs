using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

/// <summary>
/// Service for collecting raw data from external APIs (IGDB, Steam, etc.)
/// Phase 1 of the import pipeline
/// </summary>
public interface IRawDataCollector
{
    /// <summary>
    /// Collects raw game data from all configured sources
    /// </summary>
    /// <param name="job">Import job to track progress</param>
    /// <param name="ct">Cancellation token</param>
    Task CollectRawDataAsync(ImportJob job, CancellationToken ct);
}
