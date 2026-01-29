namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public class PostRepository(SnakkDbContext context)
    : GenericDatabaseRepository<PostDatabaseEntity>(context), IPostRepository
{
    public override async Task<PostDatabaseEntity?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(p => p.Discussion)
            .Include(p => p.CreatedByUser)
            .Include(p => p.ReplyToPost)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<PostDatabaseEntity?> GetForUpdateAsync(string publicId)
    {
        return await _dbSet
            .Include(p => p.Discussion)
            .Include(p => p.CreatedByUser)
            .Include(p => p.ReplyToPost)
            .FirstOrDefaultAsync(p => p.PublicId == publicId);
    }

    public override async Task<IEnumerable<PostDatabaseEntity>> GetAllAsync()
    {
        return await _dbSet
            .Include(p => p.Discussion)
            .Include(p => p.CreatedByUser)
            .Include(p => p.ReplyToPost)
            .ToListAsync();
    }

    public async Task<PostDetailDto?> GetForDisplayAsync(string publicId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(p => p.PublicId == publicId)
            .Select(p => new PostDetailDto(
                p.PublicId,
                p.Content,
                p.CreatedAt,
                p.EditedAt,
                p.IsFirstPost,
                p.Discussion.PublicId,
                p.Discussion.Title,
                p.CreatedByUser.PublicId,
                p.CreatedByUser.DisplayName,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null))
            .FirstOrDefaultAsync();
    }

    public async Task<PostDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet
            .Include(p => p.Discussion)
            .Include(p => p.CreatedByUser)
            .Include(p => p.ReplyToPost)
            .FirstOrDefaultAsync(p => p.PublicId == publicId);
    }

    public async Task<IEnumerable<PostDatabaseEntity>> GetByDiscussionIdAsync(int discussionId)
    {
        return await _dbSet
            .Include(p => p.Discussion)
            .Include(p => p.CreatedByUser)
            .Include(p => p.ReplyToPost)
            .Where(p => p.DiscussionId == discussionId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<PagedResult<PostListDto>> GetPagedByDiscussionIdAsync(
        int discussionId,
        int offset,
        int pageSize)
    {
        var items = await _dbSet
            .AsNoTracking()
            .Where(p => p.DiscussionId == discussionId)
            .OrderBy(p => p.CreatedAt)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(p => new PostListDto(
                p.PublicId,
                p.Content,
                p.CreatedAt,
                p.EditedAt,
                p.IsFirstPost,
                p.CreatedByUser.PublicId,
                p.CreatedByUser.DisplayName))
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize) : items;

        return new PagedResult<PostListDto>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task<IEnumerable<PostDatabaseEntity>> GetByUserIdAsync(int userId)
    {
        return await _dbSet
            .Where(p => p.CreatedByUserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }
}
