namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(int id);
    Task<Post?> GetByPublicIdAsync(PostId publicId);
    Task<IEnumerable<Post>> GetByPublicIdsAsync(IEnumerable<PostId> publicIds);
    Task<IEnumerable<Post>> GetByDiscussionIdAsync(DiscussionId discussionId);
    Task<PagedResult<Post>> GetPagedByDiscussionIdAsync(DiscussionId discussionId, int offset, int pageSize);
    Task<IEnumerable<Post>> GetByUserIdAsync(UserId userId);
    Task AddAsync(Post post);
    Task UpdateAsync(Post post);
    Task DeleteAsync(Post post);
    Task AddRevisionAsync(PostRevision revision);
    Task<IEnumerable<PostRevision>> GetRevisionsAsync(PostId postId);
}
