namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public interface IAdminUserRepository
{
    Task<AdminUser?> GetByIdAsync(int id);
    Task<AdminUser?> GetByPublicIdAsync(AdminUserId publicId);
    Task<AdminUser?> GetByEmailAsync(string email);
    Task<IEnumerable<AdminUser>> GetAllAsync();
    Task AddAsync(AdminUser adminUser);
    Task UpdateAsync(AdminUser adminUser);
}
