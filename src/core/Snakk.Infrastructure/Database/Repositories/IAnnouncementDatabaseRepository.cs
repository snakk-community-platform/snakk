namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IAnnouncementDatabaseRepository : IGenericDatabaseRepository<AnnouncementDatabaseEntity>
{
    Task<AnnouncementDatabaseEntity?> GetByPublicIdAsync(string publicId);
}
