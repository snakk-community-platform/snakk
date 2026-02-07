namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class AdminUserRepository(SnakkDbContext context)
    : GenericDatabaseRepository<AdminUserDatabaseEntity>(context), IAdminUserRepository
{
    public async Task<AdminUserDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet.FirstOrDefaultAsync(a => a.PublicId == publicId);
    }

    public async Task<AdminUserDatabaseEntity?> GetByEmailAsync(string email)
    {
        return await _dbSet.FirstOrDefaultAsync(a => a.Email == email.ToLower());
    }
}
