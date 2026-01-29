namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public class DiscussionRepository(SnakkDbContext context)
    : GenericDatabaseRepository<DiscussionDatabaseEntity>(context), IDiscussionRepository
{
    public override async Task<DiscussionDatabaseEntity?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(d => d.Space)
            .Include(d => d.CreatedByUser)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<DiscussionDatabaseEntity?> GetForUpdateAsync(string publicId)
    {
        return await _dbSet
            .Include(d => d.Space)
            .Include(d => d.CreatedByUser)
            .Include(d => d.Posts)
            .FirstOrDefaultAsync(d => d.PublicId == publicId);
    }

    public override async Task<IEnumerable<DiscussionDatabaseEntity>> GetAllAsync()
    {
        return await _dbSet
            .Include(d => d.Space)
            .Include(d => d.CreatedByUser)
            .ToListAsync();
    }

    public async Task<DiscussionDetailDto?> GetForDisplayAsync(string publicId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(d => d.PublicId == publicId)
            .Select(d => new DiscussionDetailDto(
                d.PublicId,
                d.Title,
                d.Slug,
                d.CreatedAt,
                d.LastActivityAt,
                d.IsPinned,
                d.IsLocked,
                d.Space.PublicId,
                d.Space.Name,
                d.CreatedByUser.PublicId,
                d.CreatedByUser.DisplayName))
            .FirstOrDefaultAsync();
    }

    public async Task<DiscussionDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet
            .Include(d => d.Space)
            .Include(d => d.CreatedByUser)
            .FirstOrDefaultAsync(d => d.PublicId == publicId);
    }

    public async Task<DiscussionDatabaseEntity?> GetBySlugAsync(string slug)
    {
        return await _dbSet
            .Include(d => d.Space)
            .Include(d => d.CreatedByUser)
            .FirstOrDefaultAsync(d => d.Slug == slug);
    }

    public async Task<IEnumerable<DiscussionDatabaseEntity>> GetBySpaceIdAsync(int spaceId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(d => d.Space)
            .Include(d => d.CreatedByUser)
            .Where(d => d.SpaceId == spaceId)
            .OrderByDescending(d => d.IsPinned)
            .ThenByDescending(d => d.LastActivityAt)
            .ToListAsync();
    }

    public async Task<PagedResult<DiscussionListDto>> GetPagedBySpaceIdAsync(
        int spaceId,
        int offset,
        int pageSize,
        string? cursor = null)
    {
        var query = _dbSet
            .AsNoTracking()
            .Where(d => d.SpaceId == spaceId);

        // Apply keyset pagination if cursor provided
        var cursorData = Cursor.Decode(cursor);
        if (cursorData.HasValue)
        {
            var (cursorDate, cursorId) = cursorData.Value;
            // For space feed: ORDER BY IsPinned DESC, LastActivityAt DESC, Id DESC
            // Keyset: WHERE (LastActivityAt < cursorDate) OR (LastActivityAt = cursorDate AND Id < cursorId)
            // Note: Pinned items are tricky - we assume cursor is within non-pinned for simplicity
            query = query.Where(d => 
                d.LastActivityAt < cursorDate || 
                (d.LastActivityAt == cursorDate && d.Id < cursorId));
        }
        else if (offset > 0)
        {
            // Fall back to offset for backwards compatibility
            query = query.Skip(offset);
        }

        var items = await query
            .OrderByDescending(d => d.IsPinned)
            .ThenByDescending(d => d.LastActivityAt)
            .ThenByDescending(d => d.Id)
            .Take(pageSize + 1)
            .Select(d => new { 
                d.Id,
                Dto = new DiscussionListDto(
                    d.PublicId,
                    d.Title,
                    d.Slug,
                    d.CreatedAt,
                    d.LastActivityAt,
                    d.IsPinned,
                    d.IsLocked,
                    d.CreatedByUser.PublicId,
                    d.CreatedByUser.DisplayName,
                    d.PostCount,
                    d.ReactionCount,
                    string.IsNullOrEmpty(d.Tags) ? Array.Empty<string>() : d.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries))
            })
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize).ToList() : items;

        // Generate next cursor from last item
        string? nextCursor = null;
        if (hasMoreItems && resultItems.Count > 0)
        {
            var lastItem = resultItems[^1];
            nextCursor = Cursor.Encode(lastItem.Dto.LastActivityAt, lastItem.Id);
        }

        return new PagedResult<DiscussionListDto>
        {
            Items = resultItems.Select(x => x.Dto),
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems,
            NextCursor = nextCursor
        };
    }

    public async Task<IEnumerable<DiscussionDatabaseEntity>> GetRecentAsync(int count)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(d => d.Space)
            .Include(d => d.CreatedByUser)
            .OrderByDescending(d => d.LastActivityAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<PagedResult<RecentDiscussionDto>> GetRecentWithDetailsAsync(
        int offset, 
        int pageSize, 
        string? communityId = null, 
        string? cursor = null)
    {
        var query = _dbSet.AsNoTracking();

        // Filter by community if specified
        if (!string.IsNullOrEmpty(communityId))
        {
            query = query.Where(d => d.Space.Hub.Community.PublicId == communityId);
        }

        // Apply keyset pagination if cursor provided
        var cursorData = Cursor.Decode(cursor);
        if (cursorData.HasValue)
        {
            var (cursorDate, cursorId) = cursorData.Value;
            // ORDER BY LastActivityAt DESC, Id DESC
            // Keyset: WHERE (LastActivityAt < cursorDate) OR (LastActivityAt = cursorDate AND Id < cursorId)
            query = query.Where(d => 
                d.LastActivityAt < cursorDate || 
                (d.LastActivityAt == cursorDate && d.Id < cursorId));
        }
        else if (offset > 0)
        {
            // Fall back to offset for backwards compatibility
            query = query.Skip(offset);
        }

        var items = await query
            .OrderByDescending(d => d.LastActivityAt)
            .ThenByDescending(d => d.Id)
            .Take(pageSize + 1)
            .Select(d => new {
                d.Id,
                Dto = new RecentDiscussionDto(
                    d.PublicId,
                    d.Title,
                    d.Slug,
                    d.CreatedAt,
                    d.LastActivityAt,
                    d.IsPinned,
                    d.IsLocked,
                    d.Space.PublicId,
                    d.Space.Slug,
                    d.Space.Name,
                    d.Space.Hub.PublicId,
                    d.Space.Hub.Slug,
                    d.Space.Hub.Name,
                    d.Space.Hub.Community.PublicId,
                    d.Space.Hub.Community.Slug,
                    d.Space.Hub.Community.Name,
                    d.CreatedByUser.PublicId,
                    d.CreatedByUser.DisplayName,
                    d.CreatedByUser.AvatarFileName,
                    d.PostCount,
                    d.ReactionCount,
                    string.IsNullOrEmpty(d.Tags) ? Array.Empty<string>() : d.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries))
            })
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize).ToList() : items;

        // Generate next cursor from last item
        string? nextCursor = null;
        if (hasMoreItems && resultItems.Count > 0)
        {
            var lastItem = resultItems[^1];
            nextCursor = Cursor.Encode(lastItem.Dto.LastActivityAt, lastItem.Id);
        }

        return new PagedResult<RecentDiscussionDto>
        {
            Items = resultItems.Select(x => x.Dto),
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems,
            NextCursor = nextCursor
        };
    }
}
