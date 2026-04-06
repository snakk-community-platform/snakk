namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public interface INotificationDatabaseRepository : IGenericDatabaseRepository<UserNotificationDatabaseEntity>
{
    Task<UserNotificationDatabaseEntity?> GetByPublicIdAsync(string publicId);
    Task<PagedResult<UserNotificationDatabaseEntity>> GetByUserIdAsync(int userId, int offset, int pageSize);
    Task<int> GetUnreadCountAsync(int userId);
    Task MarkAllAsReadAsync(int userId);
}
