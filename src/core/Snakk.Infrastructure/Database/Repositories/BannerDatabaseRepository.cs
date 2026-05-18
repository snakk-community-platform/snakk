namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database.Entities;

public class BannerDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<BannerDatabaseEntity>(context), IBannerDatabaseRepository
{
    public async Task<BannerDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default) =>
        await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.PublicId == publicId, ct);
}
