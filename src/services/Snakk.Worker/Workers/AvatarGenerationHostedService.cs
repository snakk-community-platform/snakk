namespace Snakk.Worker.Workers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;
using System.Diagnostics;

/// <summary>
/// Background service that generates all missing avatars on application startup.
/// This ensures all existing entities have pre-generated avatar files.
/// </summary>
public class AvatarGenerationHostedService(
    IServiceProvider serviceProvider,
    ILogger<AvatarGenerationHostedService> logger,
    IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Check if generation on startup is enabled
        var enabled = configuration.GetValue<bool>("AvatarSettings:GenerateOnStartup", true);

        if (!enabled)
        {
            logger.LogInformation("Avatar generation on startup is disabled");
            return;
        }

        logger.LogInformation("Starting avatar generation for existing entities...");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var scope = serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAvatarGenerationService>();

            var count = await service.GenerateAllMissingAvatarsAsync();

            stopwatch.Stop();
            logger.LogInformation(
                "Successfully generated {Count} avatars in {Duration}ms",
                count,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(
                ex,
                "Failed to generate avatars on startup after {Duration}ms",
                stopwatch.ElapsedMilliseconds);

            // Don't throw - we don't want to prevent the application from starting
            // Avatars will be generated on-demand if needed
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No cleanup needed
        return Task.CompletedTask;
    }
}
