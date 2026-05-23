namespace Snakk.Web.Services;

public class ViewCountFlushService(
    ViewCountBuffer buffer,
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ILogger<ViewCountFlushService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = configuration.GetValue("Views:FlushIntervalMinutes", 5);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await FlushAsync(stoppingToken);
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        var counts = buffer.DrainAndReset();
        if (counts.Count == 0) return;

        try
        {
            await apiClient.RecordBatchViewsAsync(counts, ct);
            logger.LogDebug("Flushed {Count} view entries to API", counts.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to flush view counts to API");
        }
    }
}
