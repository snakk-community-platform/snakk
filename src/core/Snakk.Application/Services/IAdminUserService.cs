using Snakk.Application.DTOs.Admin;

namespace Snakk.Application.Services;

/// <summary>
/// Service for admin user management operations
/// </summary>
public interface IAdminUserService
{
    Task<AdminUserDto?> GetUserByIdAsync(string userId, CancellationToken ct = default);
    Task<bool> UserExistsAsync(string userId, CancellationToken ct = default);
    Task<PaginatedResponse<AdminBanDto>> GetActiveBansAsync(int page, int pageSize, CancellationToken ct = default);
}
