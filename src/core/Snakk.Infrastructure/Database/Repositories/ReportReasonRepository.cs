namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class ReportReasonRepository(SnakkDbContext context)
    : GenericDatabaseRepository<ReportReasonDatabaseEntity>(context), IReportReasonRepository
{
    public async Task<ReportReasonDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet
            .Include(rr => rr.Community)
            .Include(rr => rr.Hub)
            .Include(rr => rr.Space)
            .FirstOrDefaultAsync(rr => rr.PublicId == publicId);
    }

    public async Task<IEnumerable<ReportReasonDatabaseEntity>> GetReasonsForScopeAsync(int? communityId = null, int? hubId = null, int? spaceId = null)
    {
        // Get global reasons + reasons at the specified scope and parent scopes
        var query = _dbSet.AsNoTracking();
        
        // Build predicate: global OR matching community OR matching hub OR matching space
        query = query.Where(rr =>
            // Global reasons (all scope fields null)
            (rr.CommunityId == null && rr.HubId == null && rr.SpaceId == null) ||
            // Community-level reasons
            (communityId.HasValue && rr.CommunityId == communityId) ||
            // Hub-level reasons
            (hubId.HasValue && rr.HubId == hubId) ||
            // Space-level reasons
            (spaceId.HasValue && rr.SpaceId == spaceId));
        
        return await query
            .OrderBy(rr => rr.DisplayOrder)
            .ThenBy(rr => rr.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReportReasonDatabaseEntity>> GetGlobalReasonsAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Where(rr => rr.CommunityId == null && rr.HubId == null && rr.SpaceId == null)
            .OrderBy(rr => rr.DisplayOrder)
            .ThenBy(rr => rr.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReportReasonDatabaseEntity>> GetReasonsByEntityAsync(int? communityId = null, int? hubId = null, int? spaceId = null)
    {
        var query = _dbSet.AsNoTracking();
        
        if (spaceId.HasValue)
            query = query.Where(rr => rr.SpaceId == spaceId);
        else if (hubId.HasValue)
            query = query.Where(rr => rr.HubId == hubId);
        else if (communityId.HasValue)
            query = query.Where(rr => rr.CommunityId == communityId);
        else
            // If no scope specified, return global reasons
            query = query.Where(rr => rr.CommunityId == null && rr.HubId == null && rr.SpaceId == null);
        
        return await query
            .OrderBy(rr => rr.DisplayOrder)
            .ThenBy(rr => rr.Name)
            .ToListAsync();
    }
}
