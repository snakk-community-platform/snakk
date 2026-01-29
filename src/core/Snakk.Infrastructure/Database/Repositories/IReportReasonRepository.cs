namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IReportReasonRepository : IGenericDatabaseRepository<ReportReasonDatabaseEntity>
{
    Task<ReportReasonDatabaseEntity?> GetByPublicIdAsync(string publicId);
    
    /// <summary>
    /// Get all reasons available for a given scope (includes global + inherited from parent scopes)
    /// </summary>
    Task<IEnumerable<ReportReasonDatabaseEntity>> GetReasonsForScopeAsync(int? communityId = null, int? hubId = null, int? spaceId = null);
    
    /// <summary>
    /// Get only global reasons (no scope)
    /// </summary>
    Task<IEnumerable<ReportReasonDatabaseEntity>> GetGlobalReasonsAsync();
    
    /// <summary>
    /// Get reasons defined at a specific scope (not inherited)
    /// </summary>
    Task<IEnumerable<ReportReasonDatabaseEntity>> GetReasonsByEntityAsync(int? communityId = null, int? hubId = null, int? spaceId = null);
}
