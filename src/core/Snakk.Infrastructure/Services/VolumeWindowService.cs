namespace Snakk.Infrastructure.Services;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Shared.Enums;

public class VolumeWindowService(
    IActivitySnapshotRepository snapshotRepository,
    HybridCache cache,
    IConfiguration configuration) : IVolumeWindowService
{
    // Aggregate: TTL-only, no write-path invalidation
    private static readonly HybridCacheEntryOptions CacheOptions = new() { Expiration = TimeSpan.FromHours(1) };

    private const int SnapshotLookbackDays = 30;
    private const int MinDataDays = 3;
    private const double P33Index = 0.33;

    public Task<TimeSpan> EstimatePostWindowAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(
            "volume-window:posts",
            factoryCt => ComputePostWindowAsync(factoryCt),
            CacheOptions,
            cancellationToken: ct).AsTask();

    public Task<TimeSpan> EstimateDiscussionWindowAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(
            "volume-window:discussions",
            factoryCt => ComputeDiscussionWindowAsync(factoryCt),
            CacheOptions,
            cancellationToken: ct).AsTask();

    private async ValueTask<TimeSpan> ComputePostWindowAsync(CancellationToken ct)
    {
        if (!IsAdaptiveEnabled())
            return FallbackWindow();

        var days = await snapshotRepository.GetSparklineAsync(ActivityEntityTypeEnum.Platform, null, SnapshotLookbackDays, ct);
        if (days.Count < MinDataDays)
            return FallbackWindow();

        var p33Volume = GetP33(days.Select(d => d.PostCount).ToList());
        if (p33Volume <= 0)
            return FallbackWindow();

        var target = configuration.GetValue("Trending:TargetPostsInWindow", 500);
        var buffer = configuration.GetValue("Trending:BufferMultiplier", 1.15);
        return Clamp(TimeSpan.FromDays(target / (double)p33Volume * buffer));
    }

    private async ValueTask<TimeSpan> ComputeDiscussionWindowAsync(CancellationToken ct)
    {
        if (!IsAdaptiveEnabled())
            return FallbackWindow();

        var days = await snapshotRepository.GetSparklineAsync(ActivityEntityTypeEnum.Platform, null, SnapshotLookbackDays, ct);
        if (days.Count < MinDataDays)
            return FallbackWindow();

        var p33Volume = GetP33(days.Select(d => d.DiscussionCount).ToList());
        if (p33Volume <= 0)
            return FallbackWindow();

        var target = configuration.GetValue("Trending:TargetDiscussionsInWindow", 100);
        var buffer = configuration.GetValue("Trending:BufferMultiplier", 1.15);
        return Clamp(TimeSpan.FromDays(target / (double)p33Volume * buffer));
    }

    private bool IsAdaptiveEnabled() =>
        configuration.GetValue("Features:AdaptiveVolumeWindowEnabled", true);

    private TimeSpan FallbackWindow() =>
        TimeSpan.FromHours(configuration.GetValue("Trending:LookbackHours", 24));

    private TimeSpan Clamp(TimeSpan window)
    {
        var min = TimeSpan.FromHours(configuration.GetValue("Trending:MinWindowHours", 6));
        var max = TimeSpan.FromDays(configuration.GetValue("Trending:MaxWindowDays", 30));
        if (window < min) return min;
        if (window > max) return max;
        return window;
    }

    private static int GetP33(List<int> values)
    {
        values.Sort();
        var index = (int)(values.Count * P33Index);
        return values[index];
    }
}
