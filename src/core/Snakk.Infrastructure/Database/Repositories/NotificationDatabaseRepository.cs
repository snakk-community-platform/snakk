namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public class NotificationDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<NotificationDatabaseEntity>(context), INotificationDatabaseRepository
{
    private readonly SnakkDbContext _context = context;

    private static readonly Func<SnakkDbContext, int, Task<int>> _getUnreadCount
        = EF.CompileAsyncQuery(
            (SnakkDbContext ctx, int userId) =>
                ctx.Notifications.Count(n => n.RecipientUserId == userId && !n.IsRead));

    public async Task<NotificationDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(n => n.PublicId == publicId);
    }

    public async Task<PagedResult<NotificationDatabaseEntity>> GetByUserIdAsync(int userId, int offset, int pageSize)
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

        return new PagedResult<NotificationDatabaseEntity>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _getUnreadCount(_context, userId);
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        await _dbSet
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }
}
