namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public class NotificationDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<NotificationDatabaseEntity>(context), INotificationDatabaseRepository
{
    public override async Task<NotificationDatabaseEntity?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(n => n.RecipientUser)
            .Include(n => n.ActorUser)
            .Include(n => n.SourcePost)
            .Include(n => n.SourceDiscussion)
            .Include(n => n.SourceSpace)
            .FirstOrDefaultAsync(n => n.Id == id);
    }

    public override async Task<IEnumerable<NotificationDatabaseEntity>> GetAllAsync()
    {
        return await _dbSet
            .Include(n => n.RecipientUser)
            .Include(n => n.ActorUser)
            .Include(n => n.SourcePost)
            .Include(n => n.SourceDiscussion)
            .Include(n => n.SourceSpace)
            .ToListAsync();
    }

    public async Task<NotificationDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet
            .Include(n => n.RecipientUser)
            .Include(n => n.ActorUser)
            .Include(n => n.SourcePost)
            .Include(n => n.SourceDiscussion)
            .Include(n => n.SourceSpace)
            .FirstOrDefaultAsync(n => n.PublicId == publicId);
    }

    public async Task<PagedResult<NotificationDatabaseEntity>> GetByUserIdAsync(int userId, int offset, int pageSize)
    {
        // Don't use Include() - the DTOs only need FK IDs which are already on the notification entity
        var items = await _dbSet
            .AsNoTracking()
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
        return await _dbSet
            .AsNoTracking()
            .CountAsync(n => n.RecipientUserId == userId && !n.IsRead);
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
