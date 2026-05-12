using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.Services;

public class EgsDataProvider : IDataProvider
{
    private readonly AppDbContext _db;
    private readonly ILogger<EgsDataProvider> _logger;

    public EgsDataProvider(AppDbContext db, ILogger<EgsDataProvider> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string Name => "egs";
    public string Phase => "egs_sync";

    public async Task<int> UpdateOffersAsync(ImportJob job, CancellationToken ct)
    {
        var message = "EGS provider is configured, but external price sync is not implemented yet";
        _db.ImportJobLogs.Add(new ImportJobLog
        {
            ImportJobId = job.ImportJobId,
            Timestamp = DateTime.UtcNow,
            Level = ImportJobLogLevel.Warning,
            Phase = Phase,
            Message = message
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogWarning("{Message}", message);
        job.EgsOffersUpdated = 0;
        return 0;
    }
}
