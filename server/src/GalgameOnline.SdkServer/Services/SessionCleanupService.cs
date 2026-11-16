using GalgameOnline.SdkServer.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GalgameOnline.SdkServer.Services;

/// <summary>Background service that deletes expired sessions every 10 minutes.</summary>
public sealed class SessionCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionCleanupService> _logger;

    public SessionCleanupService(IServiceScopeFactory scopeFactory, ILogger<SessionCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
                await CleanupOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown requested.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session cleanup failed; will retry on the next cycle.");
            }
        }
    }

    private async Task CleanupOnceAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SdkDbContext>();
        var now = DateTimeOffset.UtcNow;
        var deleted = await db.Sessions
            .Where(s => s.ExpiresAt <= now)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogInformation("Session cleanup removed {Count} expired sessions.", deleted);
        }
    }
}
