namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public interface INotificationDatabaseRepository : IGenericDatabaseRepository<NotificationDatabaseEntity>
{
    Task<NotificationDatabaseEntity?> GetByPublicIdAsync(string publicId);
    Task<PagedResult<NotificationDatabaseEntity>> GetByUserIdAsync(int userId, int offset, int pageSize);
    Task<int> GetUnreadCountAsync(int userId);
    Task MarkAllAsReadAsync(int userId);
}
