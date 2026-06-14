namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

public class AuthVersionSweeper(
    IServiceScopeFactory scopeFactory,
    IAuthVersionCache authVersionCache,
    ILogger<AuthVersionSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    // 5-second overlap to tolerate clock skew between DB writes and our poll timestamp
    private static readonly TimeSpan ClockSkewBuffer = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // On startup, look back 10 minutes to catch any version changes that happened while we were down
        var lastPoll = DateTime.UtcNow - TimeSpan.FromMinutes(10);

        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            var since = lastPoll - ClockSkewBuffer;
            lastPoll = DateTime.UtcNow;

            try
            {
                await SweepAsync(since, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "AuthVersionSweeper tick failed");
            }
        }
    }

    private async Task SweepAsync(DateTime since, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();

        var changed = await db.Users
            .Where(u => u.AuthVersionUpdatedAt > since && !u.IsDeleted)
            .Select(u => new { u.PublicId, u.AuthVersion })
            .ToListAsync(ct);

        if (changed.Count == 0) return;

        foreach (var u in changed)
            await authVersionCache.SetAsync(u.PublicId, u.AuthVersion, ct);

        logger.LogDebug("AuthVersionSweeper propagated {Count} auth version update(s)", changed.Count);
    }
}
