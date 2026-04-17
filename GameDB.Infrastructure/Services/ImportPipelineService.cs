using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace GameDB.Infrastructure.Services;

/// <summary>
/// Service for orchestrating import pipelines
/// </summary>
public class ImportPipelineService : IPipelineService
{
    private static readonly TimeSpan StalePipelineTimeout = TimeSpan.FromMinutes(15);
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImportPipelineService> _logger;
    private readonly Channel<ImportPipelineWorkItem> _pipelineChannel;

    public ImportPipelineService(
        IServiceProvider serviceProvider,
        ILogger<ImportPipelineService> logger,
        Channel<ImportPipelineWorkItem> pipelineChannel)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pipelineChannel = pipelineChannel;
    }

    public async Task<int> StartImportPipelineAsync(ImportPipelineOptions? options = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = GetDb(scope);

        // Check if a pipeline is already running
        var runningJob = await GetRunningJobAsync(db);
        
        if (runningJob != null)
        {
            if (IsStale(runningJob))
            {
                _logger.LogWarning("Detected stale pipeline {JobId}; marking as failed", runningJob.ImportJobId);
                runningJob.Status = ImportJobStatus.Failed;
                runningJob.CompletedAt = DateTime.UtcNow;
                runningJob.LastUpdatedAt = DateTime.UtcNow;
                runningJob.ErrorMessage = "Pipeline marked as failed due to stale heartbeat";
                await db.SaveChangesAsync();
                runningJob = null;
            }
        }

        if (runningJob != null)
        {
            _logger.LogWarning("Cannot start new pipeline: job {JobId} is already {Status}", 
                runningJob.ImportJobId, runningJob.Status);
            throw new InvalidOperationException(
                $"Import pipeline {runningJob.ImportJobId} is already {runningJob.Status}. " +
                "Wait for it to complete or cancel it first.");
        }

        // Create new job record
        var job = new ImportJob
        {
            Status = ImportJobStatus.Pending,
            CurrentPhase = "initializing",
            StartedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            IsSteamCatalogImport = false
        };

        db.ImportJobs.Add(job);
        await db.SaveChangesAsync();

        var pipelineId = job.ImportJobId;
        _logger.LogInformation("Created import pipeline {PipelineId}", pipelineId);

        // Send to channel for processing
        var normalizedOptions = NormalizeOptions(options);

        if (!_pipelineChannel.Writer.TryWrite(new ImportPipelineWorkItem(pipelineId, normalizedOptions)))
        {
            job.Status = ImportJobStatus.Failed;
            job.ErrorMessage = "Pipeline channel is not available";
            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            
            throw new InvalidOperationException("Pipeline channel is not available");
        }

        _logger.LogInformation("Pipeline {PipelineId} queued for processing", pipelineId);
        return pipelineId;
    }

    public async Task<PipelineStatus?> GetPipelineStatusAsync(int pipelineId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = GetDb(scope);

        var job = await db.ImportJobs.FindAsync(pipelineId);
        if (job == null) return null;

        return new PipelineStatus(
            PipelineId: job.ImportJobId,
            Status: job.Status.ToString().ToLowerInvariant(),
            Phase: job.CurrentPhase,
            TotalGames: job.SteamTotal,
            ProcessedGames: job.TotalGamesCreated + job.TotalGamesUpdated + job.TotalGamesSkipped + job.TotalGamesFailed,
            ImportedGames: job.TotalGamesCreated,
            UpdatedGames: job.TotalGamesUpdated,
            SkippedGames: job.TotalGamesSkipped,
            FailedGames: job.TotalGamesFailed,
            CreatedOffers: job.TotalOffersCreated,
            UpdatedOffers: job.TotalOffersUpdated,
            SkippedOffers: job.TotalOffersSkipped,
            FailedOffers: job.TotalOffersFailed,
            ErrorCount: job.ErrorCount,
            StartedAt: job.StartedAt,
            LastUpdatedAt: job.LastUpdatedAt,
            CompletedAt: job.CompletedAt,
            ErrorMessage: job.ErrorMessage,
            Steam: new PipelineStoreStatus(
                Total: job.SteamTotal,
                Processed: job.SteamProcessed,
                New: job.SteamImported,
                Updated: job.SteamUpdated,
                Skipped: job.SteamSkipped,
                Failed: job.SteamFailed),
            Gog: new PipelineStoreStatus(
                Total: job.GogTotal,
                Processed: job.GogProcessed,
                New: job.GogImported,
                Updated: job.GogUpdated,
                Skipped: job.GogSkipped,
                Failed: job.GogFailed),
            Egs: new PipelineStoreStatus(
                Total: job.EgsTotal,
                Processed: job.EgsProcessed,
                New: job.EgsImported,
                Updated: job.EgsUpdated,
                Skipped: job.EgsSkipped,
                Failed: job.EgsFailed)
        );
    }

    public async Task CancelPipelineAsync(int pipelineId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = GetDb(scope);

        var job = await db.ImportJobs.FindAsync(pipelineId);
        if (job == null)
        {
            _logger.LogWarning("Cannot cancel pipeline {PipelineId}: not found", pipelineId);
            return;
        }

        if (job.Status != ImportJobStatus.Running && job.Status != ImportJobStatus.Pending)
        {
            _logger.LogWarning("Cannot cancel pipeline {PipelineId}: status is {Status}", 
                pipelineId, job.Status);
            return;
        }

        job.Status = ImportJobStatus.Cancelled;
        job.CompletedAt = DateTime.UtcNow;
        job.LastUpdatedAt = DateTime.UtcNow;
        job.ErrorMessage = "Cancelled by user";
        await db.SaveChangesAsync();

        _logger.LogInformation("Pipeline {PipelineId} cancelled", pipelineId);
    }

    public async Task<bool> IsPipelineRunningAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = GetDb(scope);
        var runningJob = await GetRunningJobAsync(db);
        if (runningJob == null) return false;
        if (!IsStale(runningJob)) return true;

        runningJob.Status = ImportJobStatus.Failed;
        runningJob.CompletedAt = DateTime.UtcNow;
        runningJob.LastUpdatedAt = DateTime.UtcNow;
        runningJob.ErrorMessage = "Pipeline marked as failed due to stale heartbeat";
        await db.SaveChangesAsync();
        return false;
    }

    public async Task<int?> GetRunningPipelineIdAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = GetDb(scope);
        
        var runningJob = await GetRunningJobAsync(db);
        if (runningJob != null && IsStale(runningJob))
        {
            runningJob.Status = ImportJobStatus.Failed;
            runningJob.CompletedAt = DateTime.UtcNow;
            runningJob.LastUpdatedAt = DateTime.UtcNow;
            runningJob.ErrorMessage = "Pipeline marked as failed due to stale heartbeat";
            await db.SaveChangesAsync();
            return null;
        }

        return runningJob?.ImportJobId;
    }

    private static AppDbContext GetDb(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<AppDbContext>();

    private static Task<ImportJob?> GetRunningJobAsync(AppDbContext db) =>
        db.ImportJobs
            .Where(j => j.Status == ImportJobStatus.Running || j.Status == ImportJobStatus.Pending)
            .OrderByDescending(j => j.StartedAt)
            .FirstOrDefaultAsync();

    private static bool IsStale(ImportJob job)
    {
        var heartbeat = job.LastUpdatedAt.Ticks <= DateTime.UnixEpoch.Ticks ? job.StartedAt : job.LastUpdatedAt;
        return DateTime.UtcNow - heartbeat > StalePipelineTimeout;
    }

    private static ImportPipelineOptions NormalizeOptions(ImportPipelineOptions? options)
    {
        if (options == null) return new ImportPipelineOptions();

        var limit = options.Limit.GetValueOrDefault() > 0
            ? options.Limit
            : null;

        List<int>? igdbGameIds = null;
        if (options.IgdbGameIds is { Count: > 0 })
        {
            igdbGameIds = options.IgdbGameIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (igdbGameIds.Count == 0)
                igdbGameIds = null;
        }

        return new ImportPipelineOptions(
            Limit: limit,
            IgdbGameIds: igdbGameIds,
            OverwriteExisting: options.OverwriteExisting);
    }
}

public record ImportPipelineWorkItem(int PipelineId, ImportPipelineOptions Options);
