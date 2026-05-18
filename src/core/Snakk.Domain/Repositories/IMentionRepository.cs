namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public interface IMentionRepository
{
    Task<IEnumerable<Mention>> GetByPostIdAsync(PostId postId, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Mention> mentions, CancellationToken ct = default);
    Task DeleteByPostIdAsync(PostId postId, CancellationToken ct = default);
}
