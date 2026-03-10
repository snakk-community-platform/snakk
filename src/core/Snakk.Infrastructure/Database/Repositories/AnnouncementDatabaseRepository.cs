namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database.Entities;

public class AnnouncementDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<AnnouncementDatabaseEntity>(context), IAnnouncementDatabaseRepository
{
    public async Task<AnnouncementDatabaseEntity?> GetByPublicIdAsync(string publicId) =>
        await _dbSet
            .Include(a => a.CreatedByUser)
            .FirstOrDefaultAsync(a => a.PublicId == publicId);
}
