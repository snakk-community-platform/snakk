namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public interface INotificationDatabaseRepository : IGenericDatabaseRepository<UserNotificationDatabaseEntity>
{
    Task<UserNotificationDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default);
    Task<PagedResult<UserNotificationDatabaseEntity>> GetByUserIdAsync(int userId, int offset, int pageSize, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(int userId, CancellationToken ct = default);
}
