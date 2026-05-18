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
}
