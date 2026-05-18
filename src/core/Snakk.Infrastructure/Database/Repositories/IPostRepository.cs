namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public interface IPostRepository : IGenericDatabaseRepository<PostDatabaseEntity>
{
    Task<PostDatabaseEntity?> GetForUpdateAsync(string publicId, CancellationToken ct = default);
    Task<PostDetailDto?> GetForDisplayAsync(string publicId, CancellationToken ct = default);
    Task<PostDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default);
    Task<IEnumerable<PostDatabaseEntity>> GetByDiscussionIdAsync(int discussionId, CancellationToken ct = default);
    Task<PagedResult<PostListDto>> GetPagedByDiscussionIdAsync(int discussionId, int offset, int pageSize, CancellationToken ct = default);

}

public record PostListDto(
    string PublicId,
    string Content,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsFirstPost,
    string CreatedByUserPublicId,
    string CreatedByUserDisplayName);

public record PostDetailDto(
    string PublicId,
    string Content,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsFirstPost,
    string DiscussionPublicId,
    string DiscussionTitle,
    string CreatedByUserPublicId,
    string CreatedByUserDisplayName,
    string? ReplyToPostPublicId);
