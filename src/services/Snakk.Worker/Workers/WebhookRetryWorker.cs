using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;

namespace Snakk.Worker.Workers;

/// <summary>
/// Background worker that periodically retries failed webhook deliveries
/// </summary>
public class WebhookRetryWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebhookRetryWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1); // Run every minute

    public WebhookRetryWorker(
        IServiceProvider serviceProvider,
        ILogger<WebhookRetryWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook Retry Worker started");

        // Wait a bit before starting to allow app initialization
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessWebhookRetriesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook retries");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Webhook Retry Worker stopped");
    }

    private async Task ProcessWebhookRetriesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var webhookService = scope.ServiceProvider.GetRequiredService<IWebhookService>();

        try
        {
            await webhookService.RetryFailedDeliveriesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry webhook deliveries");
        }
    }
}
