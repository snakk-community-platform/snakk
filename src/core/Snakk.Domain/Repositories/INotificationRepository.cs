namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public interface INotificationRepository
{
    Task<Notification?> GetByPublicIdAsync(NotificationId notificationId, CancellationToken ct = default);
    Task<PagedResult<Notification>> GetByUserIdAsync(UserId userId, int offset, int pageSize, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(UserId userId, CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task UpdateAsync(Notification notification, CancellationToken ct = default);
    Task MarkAllAsReadAsync(UserId userId, CancellationToken ct = default);
}
