namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public interface IReportRepository : IGenericDatabaseRepository<ReportDatabaseEntity>
{
    Task<ReportDatabaseEntity?> GetByPublicIdAsync(string publicId);
    Task<ReportDatabaseEntity?> GetByPublicIdWithCommentsAsync(string publicId);
    
    /// <summary>
    /// Get reports by status for moderator queue
    /// Reports bubble up: space mods see space reports, hub mods see hub + space reports, etc.
    /// </summary>
    Task<PagedResult<ReportListDto>> GetReportsForCommunityAsync(int communityId, string? status, int offset, int pageSize);
    Task<PagedResult<ReportListDto>> GetReportsForHubAsync(int hubId, string? status, int offset, int pageSize);
    Task<PagedResult<ReportListDto>> GetReportsForSpaceAsync(int spaceId, string? status, int offset, int pageSize);
    
    /// <summary>
    /// Get all pending reports visible to a moderator
    /// Based on their roles, they see reports from their scope and below
    /// </summary>
    Task<int> GetPendingReportCountForModeratorAsync(int userId);
    
    /// <summary>
    /// Get reports resolved by a specific user
    /// </summary>
    Task<PagedResult<ReportListDto>> GetReportsResolvedByUserAsync(int userId, int offset, int pageSize);
}
