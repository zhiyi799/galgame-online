namespace GalgameOnline.QueryGateway;

/// <summary>Periodically drops GameServer instances that stopped sending heartbeats.</summary>
public sealed class GatewayCleanupService(
    InstanceRegistry registry,
    QueryGatewayOptions options,
    ILogger<GatewayCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.CleanupIntervalSeconds));
        var staleAfter = TimeSpan.FromSeconds(Math.Max(1, options.StaleTimeoutSeconds));

        using var timer = new PeriodicTimer(interval);
        while (true)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                    return;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var removed = registry.RemoveStale(staleAfter);
            if (removed > 0)
                logger.LogInformation("Removed {Count} stale game server instance(s)", removed);
        }
    }
}
