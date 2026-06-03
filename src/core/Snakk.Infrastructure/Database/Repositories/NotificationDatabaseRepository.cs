namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Extensions;
using Snakk.Shared.Models;

public class NotificationDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserNotificationDatabaseEntity>(context), INotificationDatabaseRepository
{

    public async Task<UserNotificationDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(n => n.PublicId == publicId, ct);

    public async Task<PagedResult<UserNotificationDatabaseEntity>> GetByUserIdAsync(
        int userId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        // Don't use Include() - the DTOs only need FK IDs which are already on the notification entity
        return await _dbSet
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToPagedResultAsync(offset, pageSize, ct);
    }

    public async Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default) =>
        await _dbSet.CountAsync(n => n.RecipientUserId == userId && !n.IsRead, ct);

    public async Task MarkAllAsReadAsync(int userId, CancellationToken ct = default)
    {
        try
        {
            await _dbSet
                .Where(n => n.RecipientUserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
        }
        catch (InvalidOperationException)
        {
            // Fallback for providers that don't support ExecuteUpdateAsync (e.g. InMemory)
            var unread = await _dbSet
                .Where(n => n.RecipientUserId == userId && !n.IsRead)
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            foreach (var notification in unread)
            {
                notification.IsRead = true;
                notification.ReadAt = now;
            }

            await _context.SaveChangesAsync(ct);
        }
    }
}
