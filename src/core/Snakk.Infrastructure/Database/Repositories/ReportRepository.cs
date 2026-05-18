namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class ReportRepository(SnakkDbContext context)
    : GenericDatabaseRepository<ReportDatabaseEntity>(context), IReportRepository
{
    public async Task<ReportDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default) => await _dbSet
        .Include(r => r.Reason)
        .FirstOrDefaultAsync(r => r.PublicId == publicId, ct);

    public async Task<ReportDatabaseEntity?> GetByPublicIdWithCommentsAsync(string publicId, CancellationToken ct = default) => await _dbSet
        .Include(r => r.ReporterUser)
        .Include(r => r.ReportedPost)
        .Include(r => r.ReportedDiscussion)
        .Include(r => r.ReportedUser)
        .Include(r => r.Reason)
        .Include(r => r.ResolvedByUser)
        .Include(r => r.Space)
        .Include(r => r.Hub)
        .Include(r => r.Community)
        .Include(r => r.Comments.Where(c => !c.IsDeleted))
            .ThenInclude(c => c.AuthorUser)
        .FirstOrDefaultAsync(r => r.PublicId == publicId, ct);

    public async Task<PagedResult<ReportListDto>> GetReportsForCommunityAsync(
        int communityId,
        int? statusId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _dbSet.Where(r => r.CommunityId == communityId);

        if (statusId.HasValue)
            query = query.Where(r => r.StatusId == statusId.Value);

        return await GetPagedReportsAsync(query, offset, pageSize, ct);
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForHubAsync(
        int hubId,
        int? statusId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        // Hub mods see reports for their hub AND all spaces within the hub
        var query = _dbSet
            .Where(r =>
                r.HubId == hubId
                || (r.SpaceId != null && r.Space!.HubId == hubId));

        if (statusId.HasValue)
            query = query.Where(r => r.StatusId == statusId.Value);

        return await GetPagedReportsAsync(query, offset, pageSize, ct);
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForSpaceAsync(
        int spaceId,
        int? statusId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _dbSet.Where(r => r.SpaceId == spaceId);

        if (statusId.HasValue)
            query = query.Where(r => r.StatusId == statusId.Value);

        return await GetPagedReportsAsync(query, offset, pageSize, ct);
    }

    public async Task<int> GetPendingReportCountForModeratorAsync(int userId, CancellationToken ct = default)
    {
        // Get user's active roles to determine their scope
        var userRoles = await _context.UserRoles
            .Where(ur =>
                ur.UserId == userId
                && ur.RevokedAt == null)
            .ToListAsync(ct);

        if (userRoles.Count == 0)
            return 0;

        // Build a single combined query instead of one query per role
        var query = _dbSet.Where(r => r.StatusId == (int)ReportStatusEnum.Pending);

        // Check for global admin first (sees everything)
        if (userRoles.Any(r => r.RoleId == (int)UserRoleTypeEnum.GlobalAdmin))
            return await query.CountAsync(ct);

        // Collect scope IDs from all roles
        var communityIds = new HashSet<int>();
        var hubIds = new HashSet<int>();
        var spaceIds = new HashSet<int>();

        foreach (var role in userRoles)
        {
            if ((role.RoleId == (int)UserRoleTypeEnum.CommunityAdmin
                || role.RoleId == (int)UserRoleTypeEnum.CommunityMod)
                && role.CommunityId.HasValue)
                communityIds.Add(role.CommunityId.Value);
            else if (role.RoleId == (int)UserRoleTypeEnum.HubMod && role.HubId.HasValue)
                hubIds.Add(role.HubId.Value);
            else if (role.RoleId == (int)UserRoleTypeEnum.SpaceMod && role.SpaceId.HasValue)
                spaceIds.Add(role.SpaceId.Value);
        }

        // Single query combining all scope filters with OR
        return await query.CountAsync(r =>
            (communityIds.Count > 0 && r.CommunityId.HasValue && communityIds.Contains(r.CommunityId.Value))
            || (hubIds.Count > 0 && (hubIds.Contains(r.HubId ?? 0) || (r.SpaceId != null && hubIds.Contains(r.Space!.HubId))))
            || (spaceIds.Count > 0 && r.SpaceId.HasValue && spaceIds.Contains(r.SpaceId.Value)), ct);
    }

    public async Task<PagedResult<ReportListDto>> GetReportsResolvedByUserAsync(
        int userId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _dbSet.Where(r => r.ResolvedByUserId == userId);

        return await GetPagedReportsAsync(query, offset, pageSize, ct);
    }

    private async Task<PagedResult<ReportListDto>> GetPagedReportsAsync(
        IQueryable<ReportDatabaseEntity> query,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .Select(r => new ReportListDto(
                r.PublicId,
                ((ReportStatusEnum)r.StatusId).ToString(),
                r.ReporterUser.PublicId,
                r.ReporterUser.DisplayName ?? "",
                r.ReportedPost != null ? r.ReportedPost.PublicId : null,
                r.ReportedPost != null
                    ? r.ReportedPost.Content.Length > 100
                        ? r.ReportedPost.Content.Substring(0, 100) + "..."
                        : r.ReportedPost.Content
                    : null,
                r.ReportedDiscussion != null ? r.ReportedDiscussion.PublicId : null,
                r.ReportedDiscussion != null ? r.ReportedDiscussion.Title : null,
                r.ReportedUser != null ? r.ReportedUser.PublicId : null,
                r.ReportedUser != null ? r.ReportedUser.DisplayName : null,
                r.Reason != null ? r.Reason.Name : null,
                r.Details,
                r.CreatedAt,
                r.ResolvedAt,
                r.ResolvedByUser != null ? r.ResolvedByUser.PublicId : null,
                r.ResolvedByUser != null ? r.ResolvedByUser.DisplayName : null,
                r.ResolutionNote,
                r.Space != null ? r.Space.PublicId : null,
                r.Space != null ? r.Space.Name : null,
                r.Hub != null ? r.Hub.PublicId : null,
                r.Hub != null ? r.Hub.Name : null,
                r.Community != null ? r.Community.PublicId : null,
                r.Community != null ? r.Community.Name : null,
                r.Comments.Count(c => !c.IsDeleted)))
            .ToListAsync(ct);

        return new PagedResult<ReportListDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = offset + items.Count < totalCount
        };
    }
}
