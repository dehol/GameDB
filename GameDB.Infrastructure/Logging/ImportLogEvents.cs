using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.Logging;

/// <summary>
/// Event IDs for structured logging of import operations.
/// Use these with ILogger.Log methods for consistent, searchable logs.
/// </summary>
public static class ImportLogEvents
{
    // Pipeline lifecycle events (1000-1099)
    public static readonly EventId PipelineStarted = new(1001, "PipelineStarted");
    public static readonly EventId PipelineCompleted = new(1002, "PipelineCompleted");
    public static readonly EventId PipelineFailed = new(1003, "PipelineFailed");
    public static readonly EventId PipelineCancelled = new(1004, "PipelineCancelled");
    public static readonly EventId PipelineQueued = new(1005, "PipelineQueued");

    // Phase events (1010-1019)
    public static readonly EventId PhaseStarted = new(1010, "PhaseStarted");
    public static readonly EventId PhaseCompleted = new(1011, "PhaseCompleted");
    public static readonly EventId PhaseFailed = new(1012, "PhaseFailed");

    // Data collection events (1020-1029)
    public static readonly EventId IgdbFetchStarted = new(1020, "IgdbFetchStarted");
    public static readonly EventId IgdbFetchCompleted = new(1021, "IgdbFetchCompleted");
    public static readonly EventId SteamFetchStarted = new(1022, "SteamFetchStarted");
    public static readonly EventId SteamFetchCompleted = new(1023, "SteamFetchCompleted");

    // Processing events (1030-1039)
    public static readonly EventId BatchProcessed = new(1030, "BatchProcessed");
    public static readonly EventId StagingStarted = new(1031, "StagingStarted");
    public static readonly EventId StagingCompleted = new(1032, "StagingCompleted");
    public static readonly EventId ImportStarted = new(1033, "ImportStarted");
    public static readonly EventId ImportCompleted = new(1034, "ImportCompleted");

    // API events (1040-1049)
    public static readonly EventId ApiCallStarted = new(1040, "ApiCallStarted");
    public static readonly EventId ApiCallCompleted = new(1041, "ApiCallCompleted");
    public static readonly EventId ApiRetry = new(1042, "ApiRetry");
    public static readonly EventId ApiFailed = new(1043, "ApiFailed");
    public static readonly EventId ApiRateLimited = new(1044, "ApiRateLimited");

    // Authentication events (1050-1059)
    public static readonly EventId AuthenticationStarted = new(1050, "AuthenticationStarted");
    public static readonly EventId AuthenticationCompleted = new(1051, "AuthenticationCompleted");
    public static readonly EventId AuthenticationFailed = new(1052, "AuthenticationFailed");
    public static readonly EventId TokenRefreshed = new(1053, "TokenRefreshed");

    // Cache events (1060-1069)
    public static readonly EventId CacheHit = new(1060, "CacheHit");
    public static readonly EventId CacheMiss = new(1061, "CacheMiss");
    public static readonly EventId CacheCleared = new(1062, "CacheCleared");
    public static readonly EventId CachePreloaded = new(1063, "CachePreloaded");

    // Error events (1070-1079)
    public static readonly EventId ProcessingError = new(1070, "ProcessingError");
    public static readonly EventId RetryAttempt = new(1071, "RetryAttempt");
    public static readonly EventId MaxRetriesExceeded = new(1072, "MaxRetriesExceeded");

    // Performance events (1080-1089)
    public static readonly EventId SlowOperation = new(1080, "SlowOperation");
    public static readonly EventId MemoryUsage = new(1081, "MemoryUsage");
    public static readonly EventId ConcurrentOperations = new(1082, "ConcurrentOperations");
}
