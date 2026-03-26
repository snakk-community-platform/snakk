namespace Snakk.Worker.Workers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;

public class OrphanMediaCleanupWorker(
    IServiceProvider serviceProvider,
    ILogger<OrphanMediaCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Media Draft Cleanup Worker started");

        // Wait 5 minutes before first execution to allow application to fully start
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();

                var count = await mediaService.CleanupExpiredDraftsAsync(stoppingToken);
                if (count > 0)
                    logger.LogInformation("Cleaned up {Count} expired media drafts", count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Media Draft Cleanup Worker");
            }

            // Run every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }

        logger.LogInformation("Media Draft Cleanup Worker stopped");
    }
}
