using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

/// <summary>
/// Service for processing raw data into staging tables
/// Phase 2 of the import pipeline
/// </summary>
public interface IDataStagingService
{
    /// <summary>
    /// Processes all unprocessed raw data into staging tables
    /// </summary>
    /// <param name="job">Import job to track progress</param>
    /// <param name="ct">Cancellation token</param>
    Task ProcessRawDataAsync(ImportJob job, CancellationToken ct);
}
