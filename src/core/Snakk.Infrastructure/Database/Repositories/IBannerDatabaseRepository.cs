namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IBannerDatabaseRepository : IGenericDatabaseRepository<BannerDatabaseEntity>
{
    Task<BannerDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default);
}
