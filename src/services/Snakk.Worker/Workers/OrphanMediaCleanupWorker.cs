namespace Snakk.Worker.Workers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

public class OrphanMediaCleanupWorker(
    IServiceProvider serviceProvider,
    ILogger<OrphanMediaCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Orphan Media Cleanup Worker started");

        // Wait 5 minutes before first execution to allow application to fully start
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOrphanMediaAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Orphan Media Cleanup Worker");
            }

            // Run every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }

        logger.LogInformation("Orphan Media Cleanup Worker stopped");
    }

    private async Task CleanupOrphanMediaAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        var cutoff = DateTime.UtcNow.AddHours(-24);

        // Find media with no PostMedia links, older than 24 hours
        var orphans = await context.Media
            .Where(m =>
                m.CreatedAt < cutoff
                && !context.PostMedia.Any(pm => pm.MediaId == m.Id))
            .Take(100) // Process in batches to avoid long transactions
            .ToListAsync(ct);

        if (orphans.Count == 0)
        {
            logger.LogDebug("No orphan media found");
            return;
        }

        logger.LogInformation("Found {Count} orphan media files to clean up", orphans.Count);

        var deletedCount = 0;
        foreach (var media in orphans)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                await fileStorage.DeleteAsync(media.StoragePath, ct);
                context.Media.Remove(media);
                deletedCount++;

                logger.LogDebug(
                    "Deleted orphan media {PublicId} ({Path})",
                    media.PublicId, media.StoragePath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to delete orphan media {PublicId} ({Path})",
                    media.PublicId, media.StoragePath);
            }
        }

        if (deletedCount > 0)
        {
            await context.SaveChangesAsync(ct);
            logger.LogInformation("Cleaned up {Count} orphan media files", deletedCount);
        }
    }
}
