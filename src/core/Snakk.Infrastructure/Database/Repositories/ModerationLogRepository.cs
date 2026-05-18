namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class ModerationLogRepository(SnakkDbContext context)
    : GenericDatabaseRepository<ModerationLogDatabaseEntity>(context), IModerationLogRepository
{
    public async Task<ModerationLogDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default) => await _dbSet
        .AsNoTracking()
        .Include(ml => ml.ActorUser)
        .Include(ml => ml.TargetPost)
        .Include(ml => ml.TargetDiscussion)
        .Include(ml => ml.TargetUser)
        .Include(ml => ml.Community)
        .Include(ml => ml.Hub)
        .Include(ml => ml.Space)
        .FirstOrDefaultAsync(ml => ml.PublicId == publicId, ct);

    public async Task<PagedResult<ModerationLogDto>> GetLogsForCommunityAsync(
        int communityId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _dbSet.Where(ml => ml.CommunityId == communityId);

        return await GetPagedLogsAsync(query, offset, pageSize, ct);
    }

    public async Task<PagedResult<ModerationLogDto>> GetLogsForHubAsync(
        int hubId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _dbSet.Where(ml => ml.HubId == hubId);

        return await GetPagedLogsAsync(query, offset, pageSize, ct);
    }

    public async Task<PagedResult<ModerationLogDto>> GetLogsForSpaceAsync(
        int spaceId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _dbSet.Where(ml => ml.SpaceId == spaceId);

        return await GetPagedLogsAsync(query, offset, pageSize, ct);
    }

    public async Task<PagedResult<ModerationLogDto>> GetLogsByActorAsync(
        int actorUserId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _dbSet.Where(ml => ml.ActorUserId == actorUserId);

        return await GetPagedLogsAsync(query, offset, pageSize, ct);
    }

    public async Task<PagedResult<ModerationLogDto>> GetLogsForTargetUserAsync(
        int targetUserId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _dbSet.Where(ml => ml.TargetUserId == targetUserId);

        return await GetPagedLogsAsync(query, offset, pageSize, ct);
    }

    private async Task<PagedResult<ModerationLogDto>> GetPagedLogsAsync(
        IQueryable<ModerationLogDatabaseEntity> query,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(ml => ml.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .Select(ml => new ModerationLogDto(
                ml.PublicId,
                ml.ActorUser.PublicId,
                ml.ActorUser.DisplayName ?? "",
                ((ModerationActionEnum)ml.ActionId).ToString(),
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
            .ToListAsync(ct);

        return new PagedResult<ModerationLogDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = offset + items.Count < totalCount
        };
    }
}
