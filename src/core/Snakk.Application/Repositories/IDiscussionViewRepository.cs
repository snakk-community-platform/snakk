namespace Snakk.Application.Repositories;

public interface IDiscussionViewRepository
{
    Task FlushViewsAsync(IReadOnlyDictionary<(string DiscussionPublicId, string CountryCode), long> counts, CancellationToken ct = default);

    Task PruneAsync(int retainDays, CancellationToken ct = default);

    Task RollupViewCountsAsync(CancellationToken ct = default);
}
