namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public class NotificationDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserNotificationDatabaseEntity>(context), INotificationDatabaseRepository
{

    public async Task<UserNotificationDatabaseEntity?> GetByPublicIdAsync(string publicId) =>
        await _dbSet.FirstOrDefaultAsync(n => n.PublicId == publicId);

    public async Task<PagedResult<UserNotificationDatabaseEntity>> GetByUserIdAsync(
        int userId,
        int offset,
        int pageSize)
    {
        // Don't use Include() - the DTOs only need FK IDs which are already on the notification entity
        var items = await _dbSet
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(offset)
            .Take(pageSize + 1)
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize) : items;

        return new PagedResult<UserNotificationDatabaseEntity>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task<int> GetUnreadCountAsync(int userId) =>
        await _dbSet.CountAsync(n => n.RecipientUserId == userId && !n.IsRead);

    public async Task MarkAllAsReadAsync(int userId)
    {
        try
        {
            await _dbSet
                .Where(n => n.RecipientUserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, DateTime.UtcNow));
        }
        catch (InvalidOperationException)
        {
            // Fallback for providers that don't support ExecuteUpdateAsync (e.g. InMemory)
            var unread = await _dbSet
                .Where(n => n.RecipientUserId == userId && !n.IsRead)
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var notification in unread)
            {
                notification.IsRead = true;
                notification.ReadAt = now;
            }

            await _context.SaveChangesAsync();
        }
    }
}
