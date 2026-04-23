namespace Snakk.Application.Repositories;

using Snakk.Shared.Enums;

public interface IActivitySnapshotRepository
{
    /// <summary>
    /// Returns daily sparkline data for the given entity, resolved by public_id.
    /// </summary>
    Task<List<ActivitySparklineDayDto>> GetSparklineAsync(
        ActivityEntityTypeEnum entityType,
        string? publicId,
        int days,
        CancellationToken ct = default);

    /// <summary>
    /// Recomputes today's and yesterday's activity counts for all active entities
    /// and upserts them into the snapshot table. Called by ActivitySnapshotWorker.
    /// </summary>
    Task RefreshSnapshotsAsync(CancellationToken ct = default);

    Task PruneAsync(int retainDays, CancellationToken ct = default);
}

public record ActivitySparklineDayDto(DateOnly Date, int PostCount, int DiscussionCount);
