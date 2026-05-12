using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace GameDB.Infrastructure.Services;

public class GogDataProvider : IDataProvider
{
    private readonly PriceSyncService _priceSyncService;
    private readonly AppDbContext _db;
    private readonly ILogger<GogDataProvider> _logger;
    private readonly ResiliencePipeline _pipeline;

    public GogDataProvider(
        PriceSyncService priceSyncService,
        AppDbContext db,
        ILogger<GogDataProvider> logger)
    {
        _priceSyncService = priceSyncService;
        _db = db;
        _logger = logger;
        _pipeline = BuildPipeline();
    }

    public string Name => "gog";
    public string Phase => "gog_sync";

    public async Task<int> UpdateOffersAsync(ImportJob job, CancellationToken ct)
    {
        var result = await _pipeline.ExecuteAsync(async token =>
            await _priceSyncService.SyncGogPricesAsync(), ct);

        job.GogOffersUpdated += result.Updated;
        await AddLogAsync(job.ImportJobId, ImportJobLogLevel.Info, $"GOG offers updated: {result.Updated}", new { result.Scanned, result.Errors }, ct);
        return result.Updated;
    }

    private static ResiliencePipeline BuildPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<Exception>()
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(20),
                ShouldHandle = new PredicateBuilder().Handle<Exception>()
            })
            .Build();
    }

    private async Task AddLogAsync(int importJobId, ImportJobLogLevel level, string message, object? data, CancellationToken ct)
    {
        _db.ImportJobLogs.Add(new ImportJobLog
        {
            ImportJobId = importJobId,
            Timestamp = DateTime.UtcNow,
            Level = level,
            Phase = Phase,
            Message = message,
            Data = data == null ? null : System.Text.Json.JsonSerializer.Serialize(data)
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("{Message}", message);
    }
}
