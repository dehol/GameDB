using GameDB.Core.Configuration;
using GameDB.Infrastructure;
using GameDB.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically syncs game prices via ITAD API.
/// Follows the same pattern as CoverRefreshWorker.
/// </summary>
public class PriceSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PriceSyncWorker> _logger;
    private readonly ImportSettings _settings;
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

    public PriceSyncWorker(
        IServiceProvider serviceProvider,
        ILogger<PriceSyncWorker> logger,
        ImportSettings settings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Price sync worker started (interval: {Interval}h)", _settings.PriceSyncIntervalHours);

        await Task.Delay(StartupDelay, stoppingToken);

        var interval = TimeSpan.FromHours(_settings.PriceSyncIntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Price sync batch failed");
            }

            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Price sync worker stopped");
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<PriceSyncService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _logger.LogInformation("Starting scheduled price sync");
        var result = await syncService.SyncAllAsync(ct);
        _logger.LogInformation(
            "Price sync completed: {Scanned} scanned, {Updated} updated, {Errors} errors",
            result.Scanned, result.Updated, result.Errors);

        // Deduplicate PriceHistory after sync
        try
        {
            await db.Database.ExecuteSqlRawAsync("CALL pr_dedup_price_history()", ct);
            _logger.LogInformation("Price history dedup completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Price history dedup failed (non-critical)");
        }
    }
}
