namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public interface IMentionRepository
{
    Task<IEnumerable<Mention>> GetByPostIdAsync(PostId postId);
    Task AddRangeAsync(IEnumerable<Mention> mentions);
    Task DeleteByPostIdAsync(PostId postId);
}
