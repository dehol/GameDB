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
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImportPipelineService> _logger;
    private readonly Channel<int> _pipelineChannel;

    public ImportPipelineService(
        IServiceProvider serviceProvider,
        ILogger<ImportPipelineService> logger,
        Channel<int> pipelineChannel)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pipelineChannel = pipelineChannel;
    }

    public async Task<int> StartImportPipelineAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = GetDb(scope);

        // Check if a pipeline is already running
        var runningJob = await GetRunningJobAsync(db);
        
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
            Status = "pending",
            CurrentPhase = "initializing",
            StartedAt = DateTime.UtcNow,
            IsSteamCatalogImport = false
        };

        db.ImportJobs.Add(job);
        await db.SaveChangesAsync();

        var pipelineId = job.ImportJobId;
        _logger.LogInformation("Created import pipeline {PipelineId}", pipelineId);

        // Send to channel for processing
        if (!_pipelineChannel.Writer.TryWrite(pipelineId))
        {
            job.Status = "failed";
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
            Status: job.Status,
            Phase: job.CurrentPhase,
            TotalGames: job.SteamTotal,
            ProcessedGames: job.SteamProcessed,
            ImportedGames: job.TotalGamesCreated,
            ErrorCount: job.ErrorCount,
            StartedAt: job.StartedAt,
            CompletedAt: job.CompletedAt,
            ErrorMessage: job.ErrorMessage
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

        if (job.Status != "running" && job.Status != "pending")
        {
            _logger.LogWarning("Cannot cancel pipeline {PipelineId}: status is {Status}", 
                pipelineId, job.Status);
            return;
        }

        job.Status = "cancelled";
        job.CompletedAt = DateTime.UtcNow;
        job.ErrorMessage = "Cancelled by user";
        await db.SaveChangesAsync();

        _logger.LogInformation("Pipeline {PipelineId} cancelled", pipelineId);
    }

    public async Task<bool> IsPipelineRunningAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = GetDb(scope);
        return await db.ImportJobs.AnyAsync(j => j.Status == "running" || j.Status == "pending");
    }

    public async Task<int?> GetRunningPipelineIdAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = GetDb(scope);
        
        var runningJob = await GetRunningJobAsync(db);
        return runningJob?.ImportJobId;
    }

    private static AppDbContext GetDb(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<AppDbContext>();

    private static Task<ImportJob?> GetRunningJobAsync(AppDbContext db) =>
        db.ImportJobs
            .Where(j => j.Status == "running" || j.Status == "pending")
            .OrderByDescending(j => j.StartedAt)
            .FirstOrDefaultAsync();
}
