namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IUserRoleRepository : IGenericDatabaseRepository<UserRoleDatabaseEntity>
{
    Task<UserRoleDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Get all active (non-revoked) roles for a user
    /// </summary>
    Task<IEnumerable<UserRoleDatabaseEntity>> GetActiveRolesForUserAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Get all active roles at a specific scope (community/hub/space)
    /// </summary>
    Task<IEnumerable<UserRoleDatabaseEntity>> GetActiveRolesForCommunityAsync(int communityId, CancellationToken ct = default);
    Task<IEnumerable<UserRoleDatabaseEntity>> GetActiveRolesForHubAsync(int hubId, CancellationToken ct = default);
    Task<IEnumerable<UserRoleDatabaseEntity>> GetActiveRolesForSpaceAsync(int spaceId, CancellationToken ct = default);
    Task<IEnumerable<UserRoleDatabaseEntity>> GetGlobalAdminsAsync(CancellationToken ct = default);

    /// <summary>
    /// Check if user has a specific role type at or above the given scope
    /// </summary>
    Task<bool> HasRoleAtOrAboveAsync(int userId, string roleType, int? communityId = null, int? hubId = null, int? spaceId = null, CancellationToken ct = default);

    /// <summary>
    /// Check if user can moderate at a given scope (any role that grants moderation powers)
    /// </summary>
    Task<bool> CanModerateAsync(int userId, int? communityId = null, int? hubId = null, int? spaceId = null, CancellationToken ct = default);

    /// <summary>
    /// Check if user can administer at a given scope (admin roles only)
    /// </summary>
    Task<bool> CanAdministerAsync(int userId, int? communityId = null, int? hubId = null, int? spaceId = null, CancellationToken ct = default);
}
