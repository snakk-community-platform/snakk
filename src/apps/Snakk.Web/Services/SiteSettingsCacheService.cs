namespace Snakk.Web.Services;

/// <summary>
/// Background service that periodically fetches the site-wide timezone from the API.
/// Provides a sync-readable cached value for use in BasePageModel.EffectiveTimezone.
/// Refreshes every 10 minutes so admin changes propagate without a restart.
/// </summary>
public class SiteSettingsCacheService(
    SnakkApiClient apiClient,
    ILogger<SiteSettingsCacheService> logger) : BackgroundService
{
    public string? CachedTimezone { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync();

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            CachedTimezone = await apiClient.GetSiteTimezoneAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh site timezone from API");
        }
    }
}
