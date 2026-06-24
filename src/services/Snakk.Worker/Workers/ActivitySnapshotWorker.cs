namespace Snakk.Worker.Workers;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snakk.Application.Repositories;

public class ActivitySnapshotWorker(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    HybridCache cache,
    ILogger<ActivitySnapshotWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Activity Snapshot Worker started");

        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        var intervalMinutes = configuration.GetValue("Activity:SnapshotIntervalMinutes", 60);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IActivitySnapshotRepository>();

                await repo.RefreshSnapshotsAsync(stoppingToken);
                await cache.RemoveByTagAsync("sparkline", stoppingToken);
                await repo.PruneAsync(90, stoppingToken);

                var viewRepo = scope.ServiceProvider.GetRequiredService<IDiscussionViewRepository>();
                await viewRepo.PruneAsync(90, stoppingToken);
                await viewRepo.RollupViewCountsAsync(stoppingToken);

                var counterRepo = scope.ServiceProvider.GetRequiredService<ICounterRepository>();
                await counterRepo.FlushPostCountsAsync(stoppingToken);
                await counterRepo.FlushDiscussionCountsAsync(stoppingToken);
                await counterRepo.FlushReactionCountsAsync(stoppingToken);
                await counterRepo.FlushFollowerCountsAsync(stoppingToken);
                await counterRepo.FlushUserCountsAsync(stoppingToken);
                await counterRepo.FlushTrendScoresAsync(stoppingToken);
                await counterRepo.FlushReadStatesAsync(stoppingToken);

                logger.LogInformation("Activity snapshots refreshed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Activity Snapshot Worker");
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }

        logger.LogInformation("Activity Snapshot Worker stopped");
    }
}
