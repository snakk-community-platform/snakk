namespace Snakk.Application.Services;

public interface IVolumeWindowService
{
    /// <summary>
    /// Returns how far back to look to find approximately the target number of posts.
    /// Uses p33 of daily volume over the last 30 days, sized for quieter-than-average days
    /// so the window is robust to traffic spikes without needing a large buffer.
    /// Falls back to Trending:LookbackHours config value when adaptive mode is off or insufficient data exists.
    /// </summary>
    Task<TimeSpan> EstimatePostWindowAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns how far back to look to find approximately the target number of discussions.
    /// Same algorithm as EstimatePostWindowAsync but uses DiscussionCount from snapshot data.
    /// Falls back to Trending:LookbackHours config value when adaptive mode is off or insufficient data exists.
    /// </summary>
    Task<TimeSpan> EstimateDiscussionWindowAsync(CancellationToken ct = default);
}
