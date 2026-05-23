namespace Snakk.Worker.Workers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snakk.Application.Repositories;

public class ActivitySnapshotWorker(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
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
                await repo.PruneAsync(90, stoppingToken);

                var viewRepo = scope.ServiceProvider.GetRequiredService<IDiscussionViewRepository>();
                await viewRepo.PruneAsync(90, stoppingToken);

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
