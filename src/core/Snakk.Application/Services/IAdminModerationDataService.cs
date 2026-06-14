using Snakk.Application.DTOs.Admin;

namespace Snakk.Application.Services;

/// <summary>
/// Data-access service for admin moderation operations that require direct DB queries
/// not covered by the existing ModerationUseCase / IAdminUserService.
/// </summary>
public interface IAdminModerationDataService
{
    /// <summary>
    /// Returns the publicId of the active (non-expired, non-revoked) ban for a given user,
    /// or null if the user is not currently banned.
    /// </summary>
    Task<string?> GetActiveBanPublicIdAsync(string userPublicId, CancellationToken ct = default);

    /// <summary>
    /// Returns the user information needed for role-management operations
    /// (database integer id + current platform-wide active role public id if any).
    /// </summary>
    Task<AdminUserRoleInfoDto?> GetUserRoleInfoAsync(string userPublicId, CancellationToken ct = default);

    /// <summary>
    /// Returns the detailed report projection used by the admin GetReport endpoint.
    /// </summary>
    Task<AdminReportDetailDto?> GetReportDetailAsync(string publicId, CancellationToken ct = default);
}
