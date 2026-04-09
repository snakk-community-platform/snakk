namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database.Entities;

public class BannerDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<BannerDatabaseEntity>(context), IBannerDatabaseRepository
{
    public async Task<BannerDatabaseEntity?> GetByPublicIdAsync(string publicId) =>
        await _dbSet
            .AsNoTracking()
            .Include(a => a.CreatedByUser)
            .FirstOrDefaultAsync(a => a.PublicId == publicId);
}
