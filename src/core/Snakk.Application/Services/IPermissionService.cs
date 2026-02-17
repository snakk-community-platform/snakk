using Snakk.Application.DTOs.Security;

namespace Snakk.Application.Services;

public interface IPermissionService
{
    /// <summary>
    /// Check if a user has a specific permission
    /// </summary>
    Task<bool> UserHasPermissionAsync(string userId, string permissionName, string? scope = null, int? scopeId = null);

    /// <summary>
    /// Get all permissions for a specific user (based on their roles)
    /// </summary>
    Task<List<PermissionDto>> GetUserPermissionsAsync(string userId);

    /// <summary>
    /// Get all permissions assigned to a specific role
    /// </summary>
    Task<List<PermissionDto>> GetRolePermissionsAsync(int roleId);

    /// <summary>
    /// Get all available system permissions
    /// </summary>
    Task<List<PermissionDto>> GetAllPermissionsAsync();

    /// <summary>
    /// Assign a permission to a role
    /// </summary>
    Task AssignPermissionToRoleAsync(int roleId, int permissionId, string adminUserId);

    /// <summary>
    /// Revoke a permission from a role
    /// </summary>
    Task RevokePermissionFromRoleAsync(int roleId, int permissionId, string adminUserId);

    /// <summary>
    /// Grant a temporary role elevation to a user
    /// </summary>
    Task<TemporaryRoleElevationDto> GrantTemporaryRoleAsync(
        string userId,
        string roleType,
        string scope,
        int scopeId,
        DateTime expiresAt,
        string reason,
        string adminUserId);

    /// <summary>
    /// Revoke a temporary role elevation
    /// </summary>
    Task RevokeTemporaryRoleAsync(string elevationId, string reason, string adminUserId);

    /// <summary>
    /// Get all active temporary role elevations
    /// </summary>
    Task<List<TemporaryRoleElevationDto>> GetActiveTemporaryRolesAsync();

    /// <summary>
    /// Get temporary role elevations for a specific user
    /// </summary>
    Task<List<TemporaryRoleElevationDto>> GetUserTemporaryRolesAsync(string userId);
}
