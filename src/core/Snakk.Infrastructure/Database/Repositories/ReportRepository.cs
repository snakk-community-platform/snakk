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
    public async Task<ReportDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet
            .Include(r => r.ReporterUser)
            .Include(r => r.ReportedPost)
            .Include(r => r.ReportedDiscussion)
            .Include(r => r.ReportedUser)
            .Include(r => r.Reason)
            .Include(r => r.ResolvedByUser)
            .Include(r => r.Space)
            .Include(r => r.Hub)
            .Include(r => r.Community)
            .FirstOrDefaultAsync(r => r.PublicId == publicId);
    }

    public async Task<ReportDatabaseEntity?> GetByPublicIdWithCommentsAsync(string publicId)
    {
        return await _dbSet
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
            .FirstOrDefaultAsync(r => r.PublicId == publicId);
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForCommunityAsync(int communityId, string? status, int offset, int pageSize)
    {
        var query = _dbSet.AsNoTracking()
            .Where(r => r.CommunityId == communityId);
        
        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status.Name == status);
        
        return await GetPagedReportsAsync(query, offset, pageSize);
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForHubAsync(int hubId, string? status, int offset, int pageSize)
    {
        // Hub mods see reports for their hub AND all spaces within the hub
        var query = _dbSet.AsNoTracking()
            .Where(r => r.HubId == hubId || (r.SpaceId != null && r.Space!.HubId == hubId));
        
        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status.Name == status);
        
        return await GetPagedReportsAsync(query, offset, pageSize);
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForSpaceAsync(int spaceId, string? status, int offset, int pageSize)
    {
        var query = _dbSet.AsNoTracking()
            .Where(r => r.SpaceId == spaceId);
        
        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status.Name == status);
        
        return await GetPagedReportsAsync(query, offset, pageSize);
    }

    public async Task<int> GetPendingReportCountForModeratorAsync(int userId)
    {
        // Get user's active roles to determine their scope
        var userRoles = await _context.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.RevokedAt == null)
            .ToListAsync();
        
        if (!userRoles.Any())
            return 0;
        
        // Build query based on roles
        var query = _dbSet.AsNoTracking()
            .Where(r => r.StatusId == (int)ReportStatusEnum.Pending);
        
        var reportIds = new HashSet<int>();
        
        foreach (var role in userRoles)
        {
            IQueryable<ReportDatabaseEntity> roleQuery;

            if (role.RoleId == (int)UserRoleTypeEnum.GlobalAdmin)
            {
                // Global admins see all pending reports
                return await query.CountAsync();
            }
            else if (role.RoleId == (int)UserRoleTypeEnum.CommunityAdmin || role.RoleId == (int)UserRoleTypeEnum.CommunityMod)
            {
                roleQuery = query.Where(r => r.CommunityId == role.CommunityId);
            }
            else if (role.RoleId == (int)UserRoleTypeEnum.HubMod)
            {
                roleQuery = query.Where(r =>
                    r.HubId == role.HubId ||
                    (r.SpaceId != null && r.Space!.HubId == role.HubId));
            }
            else if (role.RoleId == (int)UserRoleTypeEnum.SpaceMod)
            {
                roleQuery = query.Where(r => r.SpaceId == role.SpaceId);
            }
            else
            {
                continue;
            }
            
            var ids = await roleQuery.Select(r => r.Id).ToListAsync();
            foreach (var id in ids)
                reportIds.Add(id);
        }
        
        return reportIds.Count;
    }

    public async Task<PagedResult<ReportListDto>> GetReportsResolvedByUserAsync(int userId, int offset, int pageSize)
    {
        var query = _dbSet.AsNoTracking()
            .Where(r => r.ResolvedByUserId == userId);
        
        return await GetPagedReportsAsync(query, offset, pageSize);
    }

    private async Task<PagedResult<ReportListDto>> GetPagedReportsAsync(IQueryable<ReportDatabaseEntity> query, int offset, int pageSize)
    {
        var totalCount = await query.CountAsync();
        
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .Select(r => new ReportListDto(
                r.PublicId,
                r.Status.Name,
                r.ReporterUser.PublicId,
                r.ReporterUser.DisplayName,
                r.ReportedPost != null ? r.ReportedPost.PublicId : null,
                r.ReportedPost != null ? (r.ReportedPost.Content.Length > 100 ? r.ReportedPost.Content.Substring(0, 100) + "..." : r.ReportedPost.Content) : null,
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
            .ToListAsync();
        
        return new PagedResult<ReportListDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = offset + items.Count < totalCount
        };
    }
}
