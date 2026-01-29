namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public record ReadStateWithPostNumber(string DiscussionId, int LastReadPostNumber);

public interface IDiscussionReadStateRepository
{
    Task<DiscussionReadState?> GetAsync(UserId userId, DiscussionId discussionId);
    Task SaveAsync(DiscussionReadState readState);
    Task<List<ReadStateWithPostNumber>> GetReadStatesForDiscussionsAsync(UserId userId, List<string> discussionIds);
}
