using GameDB.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically resolves missing cover URLs
/// and caches them to Game.CoverUrl
/// </summary>
public class CoverRefreshWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CoverRefreshWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);
    private const int BatchSize = 50;

    public CoverRefreshWorker(
        IServiceProvider serviceProvider,
        ILogger<CoverRefreshWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cover refresh worker started");

        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshMissingCoversAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cover refresh batch failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }

        _logger.LogInformation("Cover refresh worker stopped");
    }

    private async Task RefreshMissingCoversAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var gameIds = await db.Games
            .Where(g => g.CoverUrl == null)
            .OrderBy(g => g.GameId)
            .Select(g => g.GameId)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (gameIds.Count == 0) return;

        _logger.LogInformation("Refreshing covers for {Count} games without cached URLs", gameIds.Count);

        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
        var resolved = await gameService.GetCoverSourcesAsync(gameIds);

        _logger.LogInformation("Resolved {Resolved} of {Total} cover URLs", resolved.Count, gameIds.Count);
    }
}
