using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.Services;

public class GogApiService : IGogApiService
{
    private readonly ILogger<GogApiService> _logger;

    public GogApiService(ILogger<GogApiService> logger)
    {
        _logger = logger;
    }

    public Task<List<GameWishlistItem>> GetWishlistAsync(string gogUserId, CancellationToken ct = default)
    {
        const string message = "GOG wishlist import is not implemented yet.";
        _logger.LogWarning("{Message} GogUserId={GogUserId}", message, gogUserId);
        throw new NotSupportedException(message);
    }
}
