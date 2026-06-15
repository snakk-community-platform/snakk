namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public record ReadStateWithPostNumber(string DiscussionId, int LastReadPostNumber);

public interface IDiscussionReadStateRepository
{
    Task<DiscussionReadState?> GetAsync(UserId userId, DiscussionId discussionId, CancellationToken ct = default);
    Task SaveAsync(DiscussionReadState readState, CancellationToken ct = default);
    Task BatchSaveAsync(IEnumerable<DiscussionReadState> readStates, CancellationToken ct = default);
    Task<List<ReadStateWithPostNumber>> GetReadStatesForDiscussionsAsync(UserId userId, List<string> discussionIds, CancellationToken ct = default);

    /// <summary>Returns the LastReadAt timestamp per discussion for the given user (only discussions with a read state row are included).</summary>
    Task<Dictionary<string, DateTime>> GetLastReadAtByDiscussionAsync(string userId, List<string> discussionIds, CancellationToken ct = default);

    /// <summary>Returns the number of unread posts per discussion, given a per-discussion cutoff timestamp. Only discussions with count &gt; 0 are included.</summary>
    Task<Dictionary<string, int>> GetUnreadPostCountsAsync(Dictionary<string, DateTime> cutoffByDiscussionId, CancellationToken ct = default);
}
