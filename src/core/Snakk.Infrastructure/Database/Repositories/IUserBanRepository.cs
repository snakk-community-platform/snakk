namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IUserBanRepository : IGenericDatabaseRepository<UserBanDatabaseEntity>
{
    Task<UserBanDatabaseEntity?> GetByPublicIdAsync(string publicId);
    
    /// <summary>
    /// Get all active bans for a user (not unbanned and not expired)
    /// </summary>
    Task<IEnumerable<UserBanDatabaseEntity>> GetActiveBansForUserAsync(int userId);
    
    /// <summary>
    /// Check if user is banned at a given scope (considering ban inheritance)
    /// Checks: platform-wide ban, community ban (if communityId provided), 
    /// hub ban (if hubId provided), space ban (if spaceId provided)
    /// </summary>
    Task<UserBanDatabaseEntity?> GetActiveBanForScopeAsync(int userId, int? communityId = null, int? hubId = null, int? spaceId = null);
    
    /// <summary>
    /// Check if user is banned (write or read+write) at a scope
    /// </summary>
    Task<bool> IsUserBannedAsync(int userId, int? communityId = null, int? hubId = null, int? spaceId = null);
    
    /// <summary>
    /// Get all bans issued at a specific scope
    /// </summary>
    Task<IEnumerable<UserBanDatabaseEntity>> GetBansForCommunityAsync(int communityId);
    Task<IEnumerable<UserBanDatabaseEntity>> GetBansForHubAsync(int hubId);
    Task<IEnumerable<UserBanDatabaseEntity>> GetBansForSpaceAsync(int spaceId);
}
