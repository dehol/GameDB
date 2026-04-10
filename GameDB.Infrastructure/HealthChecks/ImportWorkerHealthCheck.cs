using GameDB.Core.Configuration;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GameDB.Infrastructure.HealthChecks;

public class ImportWorkerHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;
    private readonly ImportSettings _settings;

    public ImportWorkerHealthCheck(AppDbContext db, ImportSettings settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check for stuck jobs (running longer than timeout)
            var stuckJobs = await _db.ImportJobs
                .Where(j => j.Status == "running" &&
                            j.StartedAt < DateTime.UtcNow.AddMinutes(-_settings.JobTimeoutMinutes))
                .CountAsync(cancellationToken);

            if (stuckJobs > 0)
            {
                return HealthCheckResult.Unhealthy(
                    $"{stuckJobs} import job(s) are stuck (running > {_settings.JobTimeoutMinutes} minutes). " +
                    "Manual intervention may be required.",
                    data: new Dictionary<string, object>
                    {
                        ["stuck_jobs"] = stuckJobs,
                        ["timeout_minutes"] = _settings.JobTimeoutMinutes
                    });
            }

            // Check for failed jobs in last hour
            var recentFailures = await _db.ImportJobs
                .Where(j => j.Status == "failed" &&
                            j.StartedAt > DateTime.UtcNow.AddHours(-1))
                .CountAsync(cancellationToken);

            if (recentFailures > 5)
            {
                return HealthCheckResult.Degraded(
                    $"{recentFailures} import job(s) failed in the last hour. " +
                    "Consider investigating the failures.",
                    data: new Dictionary<string, object>
                    {
                        ["recent_failures"] = recentFailures
                    });
            }

            // Check for high errorCount in recent jobs
            var recentJobs = await _db.ImportJobs
                .Where(j => j.StartedAt > DateTime.UtcNow.AddHours(-2))
                .ToListAsync(cancellationToken);

            var totalErrors = recentJobs.Sum(j => j.ErrorCount);
            var totalProcessed = recentJobs.Sum(j => j.SteamProcessed);

            if (totalProcessed > 0 && totalErrors > 0)
            {
                var errorRate = (double)totalErrors / totalProcessed;
                if (errorRate > 0.1) // More than 10% error rate
                {
                    return HealthCheckResult.Degraded(
                        $"High error rate: {errorRate:P1} ({totalErrors} errors in {totalProcessed} processed items)",
                        data: new Dictionary<string, object>
                        {
                            ["error_rate"] = errorRate,
                            ["total_errors"] = totalErrors,
                            ["total_processed"] = totalProcessed
                        });
                }
            }

            // Get current status
            var runningJobs = await _db.ImportJobs
                .CountAsync(j => j.Status == "running", cancellationToken);

            var pendingJobs = await _db.ImportJobs
                .CountAsync(j => j.Status == "pending", cancellationToken);

            return HealthCheckResult.Healthy(
                $"Import worker is operating normally. Running: {runningJobs}, Pending: {pendingJobs}",
                data: new Dictionary<string, object>
                {
                    ["running_jobs"] = runningJobs,
                    ["pending_jobs"] = pendingJobs,
                    ["recent_failures"] = recentFailures
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Failed to check import worker health",
                ex);
        }
    }
}
