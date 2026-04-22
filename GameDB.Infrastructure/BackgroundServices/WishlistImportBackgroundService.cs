using GameDB.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace GameDB.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that processes queued wishlist import requests asynchronously.
/// Accepts import work items via <see cref="EnqueueImportAsync"/> and processes them
/// sequentially using a scoped <see cref="IWishlistImportService"/> instance.
/// </summary>
public class WishlistImportBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WishlistImportBackgroundService> _logger;
    private readonly Channel<WishlistImportRequest> _channel;

    public WishlistImportBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<WishlistImportBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _channel = Channel.CreateUnbounded<WishlistImportRequest>();
    }

    /// <summary>
    /// Enqueues a wishlist import request for background processing.
    /// </summary>
    /// <param name="userId">Internal application user ID.</param>
    /// <param name="shopId">Internal shop/platform ID.</param>
    /// <param name="externalUserId">User identifier on the external platform.</param>
    /// <param name="accessToken">Optional OAuth access token (required for EGS).</param>
    public async Task EnqueueImportAsync(
        int userId,
        int shopId,
        string externalUserId,
        string? accessToken = null)
    {
        var request = new WishlistImportRequest(userId, shopId, externalUserId, accessToken);
        await _channel.Writer.WriteAsync(request);

        _logger.LogInformation(
            "Queued wishlist import for user {UserId} from shop {ShopId}",
            userId, shopId);
    }

    /// <summary>
    /// Reads and processes import requests from the channel until the service is stopped.
    /// </summary>
    /// <param name="stoppingToken">Triggered when the host is performing a graceful shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Wishlist Import Background Service started");

        try
        {
            await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IWishlistImportService>();

                    _logger.LogInformation(
                        "Processing background wishlist import for user {UserId} from shop {ShopId}",
                        request.UserId, request.ShopId);

                    await service.ImportUserWishlistAsync(
                        request.UserId,
                        request.ShopId,
                        request.ExternalUserId,
                        request.AccessToken,
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Service is shutting down — stop processing
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing background wishlist import for user {UserId}",
                        request.UserId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on graceful shutdown
        }

        _logger.LogInformation("Wishlist Import Background Service stopped");
    }
}

/// <summary>
/// Represents a queued wishlist import request.
/// </summary>
/// <param name="UserId">Internal application user ID.</param>
/// <param name="ShopId">Internal shop/platform ID.</param>
/// <param name="ExternalUserId">User identifier on the external platform.</param>
/// <param name="AccessToken">Optional OAuth access token (required for EGS).</param>
public record WishlistImportRequest(
    int UserId,
    int ShopId,
    string ExternalUserId,
    string? AccessToken);
