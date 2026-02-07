namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IAdminUserRepository : IGenericDatabaseRepository<AdminUserDatabaseEntity>
{
    Task<AdminUserDatabaseEntity?> GetByPublicIdAsync(string publicId);
    Task<AdminUserDatabaseEntity?> GetByEmailAsync(string email);
}
