using Snakk.Application.DTOs.Admin;

namespace Snakk.Application.Services;

/// <summary>
/// Service for admin user management operations
/// </summary>
public interface IAdminUserService
{
    Task<AdminUserDto?> GetUserByIdAsync(string userId);
    Task<bool> UserExistsAsync(string userId);
    Task<PaginatedResponse<AdminBanDto>> GetActiveBansAsync(int page, int pageSize);
}
