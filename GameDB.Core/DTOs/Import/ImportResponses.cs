namespace GameDB.Core.DTOs.Import;

/// <summary>
/// Response for starting a new import pipeline
/// </summary>
public record ImportStartResponse
{
    public int PipelineId { get; init; }
    public string Message { get; init; } = "Import pipeline started successfully";
    public string StatusUrl { get; init; } = null!;
}

/// <summary>
/// Response for import job history item
/// </summary>
public record ImportJobSummary
{
    public int ImportJobId { get; init; }
    public string Status { get; init; } = null!;
    public string? CurrentPhase { get; init; }
    public bool IsSteamCatalogImport { get; init; }
    public int TotalGames { get; init; }
    public int ProcessedGames { get; init; }
    public int TotalGamesCreated { get; init; }
    public int TotalOffersCreated { get; init; }
    public int ErrorCount { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public double DurationSeconds { get; init; }
}

/// <summary>
/// Paginated response for import job history
/// </summary>
public record ImportJobHistoryResponse
{
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public List<ImportJobSummary> Jobs { get; init; } = new();
}

/// <summary>
/// Response for import statistics
/// </summary>
public record ImportStatsResponse
{
    public int TotalImports { get; init; }
    public int SuccessfulImports { get; init; }
    public int FailedImports { get; init; }
    public int TotalGamesImported { get; init; }
    public int TotalOffersCreated { get; init; }
    public double SuccessRate { get; init; }
    public LastImportSummary? LastImport { get; init; }
}

/// <summary>
/// Summary of the last completed import
/// </summary>
public record LastImportSummary
{
    public int ImportJobId { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int TotalGamesCreated { get; init; }
    public int TotalOffersCreated { get; init; }
}

/// <summary>
/// Response when no pipeline is running
/// </summary>
public record NoRunningPipelineResponse
{
    public string Status { get; init; } = "no_running_pipeline";
    public string Message { get; init; } = "No import pipeline is currently running";
}

/// <summary>
/// Response when no imports have been completed
/// </summary>
public record NeverCompletedResponse
{
    public string Status { get; init; } = "never_completed";
    public string Message { get; init; } = "No import jobs have been completed yet";
}

/// <summary>
/// Error response for API errors
/// </summary>
public record ErrorResponse
{
    public string Error { get; init; } = null!;
}
