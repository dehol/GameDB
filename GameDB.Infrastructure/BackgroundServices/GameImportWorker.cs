using GameDB.Core.Models;
using GameDB.Infrastructure;
using GameDB.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace GameDB.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that processes import pipelines
/// Simplified: uses unified GameImportService instead of 3 separate services
/// </summary>
public class GameImportWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GameImportWorker> _logger;
    private readonly Channel<ImportPipelineWorkItem> _pipelineChannel;

    public GameImportWorker(
        IServiceProvider serviceProvider,
        ILogger<GameImportWorker> logger,
        Channel<ImportPipelineWorkItem> pipelineChannel)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pipelineChannel = pipelineChannel;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🎮 Game Import Worker started");

        await foreach (var workItem in _pipelineChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessPipelineAsync(workItem, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Game Import Worker stopped due to application shutdown");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Pipeline {PipelineId} failed with unhandled exception", workItem.PipelineId);
            }
        }

        _logger.LogInformation("🎮 Game Import Worker stopped");
    }

    private async Task ProcessPipelineAsync(ImportPipelineWorkItem workItem, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importService = scope.ServiceProvider.GetRequiredService<GameImportService>();
        var dataProviders = scope.ServiceProvider.GetServices<GameDB.Core.Interfaces.IDataProvider>();

        var pipelineId = workItem.PipelineId;
        var job = await db.ImportJobs.FindAsync(pipelineId, ct);
        if (job == null)
        {
            _logger.LogError("Pipeline {PipelineId} not found in database", pipelineId);
            return;
        }

        var startTime = DateTime.UtcNow;
        _logger.LogInformation("🚀 Starting pipeline {PipelineId}", pipelineId);

        try
        {
            // Run unified import
            await importService.RunImportAsync(job, workItem.Options, dataProviders, ct);
            
            _logger.LogInformation(
                "✅ Pipeline {PipelineId} completed in {Duration:mm\\:ss}. " +
                "Games: {GamesCreated}, Offers: {OffersCreated}, Errors: {Errors}",
                pipelineId, DateTime.UtcNow - startTime, 
                job.TotalGamesCreated, job.TotalOffersCreated, job.ErrorCount);
        }
        catch (OperationCanceledException)
        {
            await MarkJobCancelledAsync(job, db, ct);
            throw;
        }
        catch (Exception ex)
        {
            await MarkJobFailedAsync(job, db, ex, ct);
            _logger.LogError(ex, "❌ Pipeline {PipelineId} failed", pipelineId);
        }
    }

    private static async Task MarkJobCancelledAsync(ImportJob job, AppDbContext db, CancellationToken ct)
    {
        job.Status = ImportJobStatus.Cancelled;
        job.CompletedAt = DateTime.UtcNow;
        job.ErrorMessage = "Operation cancelled";
        await db.SaveChangesAsync(ct);
    }

    private static async Task MarkJobFailedAsync(ImportJob job, AppDbContext db, Exception ex, CancellationToken ct)
    {
        job.Status = ImportJobStatus.Failed;
        job.CompletedAt = DateTime.UtcNow;
        job.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
        await db.SaveChangesAsync(ct);
    }
}
