namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Extensions;
using Snakk.Shared.Models;

public class PostRepository(SnakkDbContext context)
    : GenericDatabaseRepository<PostDatabaseEntity>(context), IPostRepository
{
    public override async Task<PostDatabaseEntity?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<PostDatabaseEntity?> GetForUpdateAsync(string publicId, CancellationToken ct = default) => await _dbSet
        .AsTracking()
        .Include(p => p.Discussion)
        .Include(p => p.CreatedByUser)
        .Include(p => p.ReplyToPost)
        .FirstOrDefaultAsync(p => p.PublicId == publicId, ct);

    public override async Task<IEnumerable<PostDatabaseEntity>> GetAllAsync(CancellationToken ct = default) =>
        await _dbSet.AsNoTracking()
            .Include(p => p.Discussion)
            .Include(p => p.CreatedByUser)
            .Take(1000)
            .ToListAsync(ct);

    public async Task<PostDetailDto?> GetForDisplayAsync(string publicId, CancellationToken ct = default) => await _dbSet
        .Where(p => p.PublicId == publicId)
        .Select(p => new PostDetailDto(
            p.PublicId,
            p.Content,
            p.CreatedAt,
            p.EditedAt,
            p.IsFirstPost,
            p.DiscussionPublicId,
            p.Discussion.Title,
            p.CreatedByUserPublicId,
            p.CreatedByUser.DisplayName ?? "",
            p.ReplyToPost != null ? p.ReplyToPost.PublicId : null))
        .FirstOrDefaultAsync(ct);

    public async Task<PostDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(p => p.PublicId == publicId, ct);

    public async Task<IEnumerable<PostDatabaseEntity>> GetByDiscussionIdAsync(int discussionId, CancellationToken ct = default) => await _dbSet
        .AsNoTracking()
        .Where(p => p.DiscussionId == discussionId)
        .OrderBy(p => p.CreatedAt)
        .ToListAsync(ct);

    public async Task<PagedResult<PostListDto>> GetPagedByDiscussionIdAsync(
        int discussionId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        return await _dbSet
            .Where(p => p.DiscussionId == discussionId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PostListDto(
                p.PublicId,
                p.Content,
                p.CreatedAt,
                p.EditedAt,
                p.IsFirstPost,
                p.CreatedByUserPublicId,
                p.CreatedByUser.DisplayName ?? ""))
            .ToPagedResultAsync(offset, pageSize, ct);
    }
}
