using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

/// <summary>
/// Service for importing staged data into main tables
/// Phase 3 of the import pipeline
/// </summary>
public interface IDataImportService
{
    /// <summary>
    /// Imports all unprocessed staged games into main tables
    /// </summary>
    /// <param name="job">Import job to track progress</param>
    /// <param name="ct">Cancellation token</param>
    Task ImportStagedDataAsync(ImportJob job, CancellationToken ct);
}
