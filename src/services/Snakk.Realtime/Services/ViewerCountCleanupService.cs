using Snakk.Realtime.Hubs;

namespace Snakk.Realtime.Services;

/// <summary>
/// Periodically cleans up stale viewer count entries from RealtimeHub.
/// Protects against unbounded dictionary growth when connections drop
/// without triggering OnDisconnectedAsync (network failures, process kills).
/// </summary>
public class ViewerCountCleanupService(ILogger<ViewerCountCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);

            var removed = RealtimeHub.CleanupStaleEntries();

            if (removed > 0)
                logger.LogInformation("Cleaned up {Count} stale viewer count entries", removed);
        }
    }
}
