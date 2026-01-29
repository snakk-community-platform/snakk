namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public class ModerationLogRepository(SnakkDbContext context)
    : GenericDatabaseRepository<ModerationLogDatabaseEntity>(context), IModerationLogRepository
{
    public async Task<ModerationLogDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet
            .Include(ml => ml.ActorUser)
            .Include(ml => ml.TargetPost)
            .Include(ml => ml.TargetDiscussion)
            .Include(ml => ml.TargetUser)
            .Include(ml => ml.Community)
            .Include(ml => ml.Hub)
            .Include(ml => ml.Space)
            .FirstOrDefaultAsync(ml => ml.PublicId == publicId);
    }

    public async Task<PagedResult<ModerationLogDto>> GetLogsForCommunityAsync(int communityId, int offset, int pageSize)
    {
        var query = _dbSet.AsNoTracking()
            .Where(ml => ml.CommunityId == communityId);
        
        return await GetPagedLogsAsync(query, offset, pageSize);
    }

    public async Task<PagedResult<ModerationLogDto>> GetLogsForHubAsync(int hubId, int offset, int pageSize)
    {
        var query = _dbSet.AsNoTracking()
            .Where(ml => ml.HubId == hubId);
        
        return await GetPagedLogsAsync(query, offset, pageSize);
    }

    public async Task<PagedResult<ModerationLogDto>> GetLogsForSpaceAsync(int spaceId, int offset, int pageSize)
    {
        var query = _dbSet.AsNoTracking()
            .Where(ml => ml.SpaceId == spaceId);
        
        return await GetPagedLogsAsync(query, offset, pageSize);
    }

    public async Task<PagedResult<ModerationLogDto>> GetLogsByActorAsync(int actorUserId, int offset, int pageSize)
    {
        var query = _dbSet.AsNoTracking()
            .Where(ml => ml.ActorUserId == actorUserId);
        
        return await GetPagedLogsAsync(query, offset, pageSize);
    }

    public async Task<PagedResult<ModerationLogDto>> GetLogsForTargetUserAsync(int targetUserId, int offset, int pageSize)
    {
        var query = _dbSet.AsNoTracking()
            .Where(ml => ml.TargetUserId == targetUserId);
        
        return await GetPagedLogsAsync(query, offset, pageSize);
    }

    private async Task<PagedResult<ModerationLogDto>> GetPagedLogsAsync(IQueryable<ModerationLogDatabaseEntity> query, int offset, int pageSize)
    {
        var totalCount = await query.CountAsync();
        
        var items = await query
            .OrderByDescending(ml => ml.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .Select(ml => new ModerationLogDto(
                ml.PublicId,
                ml.ActorUser.PublicId,
                ml.ActorUser.DisplayName,
                ml.Action,
                ml.TargetPost != null ? ml.TargetPost.PublicId : null,
                ml.TargetDiscussion != null ? ml.TargetDiscussion.PublicId : null,
                ml.TargetDiscussion != null ? ml.TargetDiscussion.Title : null,
                ml.TargetUser != null ? ml.TargetUser.PublicId : null,
                ml.TargetUser != null ? ml.TargetUser.DisplayName : null,
                ml.Community != null ? ml.Community.PublicId : null,
                ml.Community != null ? ml.Community.Name : null,
                ml.Hub != null ? ml.Hub.PublicId : null,
                ml.Hub != null ? ml.Hub.Name : null,
                ml.Space != null ? ml.Space.PublicId : null,
                ml.Space != null ? ml.Space.Name : null,
                ml.Details,
                ml.Reason,
                ml.CreatedAt))
            .ToListAsync();
        
        return new PagedResult<ModerationLogDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = offset + items.Count < totalCount
        };
    }
}
