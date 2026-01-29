namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public interface INotificationRepository
{
    Task<Notification?> GetByPublicIdAsync(NotificationId notificationId);
    Task<PagedResult<Notification>> GetByUserIdAsync(UserId userId, int offset, int pageSize);
    Task<int> GetUnreadCountAsync(UserId userId);
    Task AddAsync(Notification notification);
    Task UpdateAsync(Notification notification);
    Task MarkAllAsReadAsync(UserId userId);
}
