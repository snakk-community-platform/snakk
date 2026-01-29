namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Models;

public class NotificationRepositoryAdapter(
    INotificationDatabaseRepository databaseRepository,
    SnakkDbContext context) : INotificationRepository
{
    private readonly INotificationDatabaseRepository _databaseRepository = databaseRepository;
    private readonly SnakkDbContext _context = context;

    public async Task<Notification?> GetByPublicIdAsync(NotificationId notificationId)
    {
        var entity = await _databaseRepository.GetByPublicIdAsync(notificationId.Value);
        return entity?.FromPersistence();
    }

    public async Task<PagedResult<Notification>> GetByUserIdAsync(UserId userId, int offset, int pageSize)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        if (user == null)
            return new PagedResult<Notification> { Items = [], Offset = offset, PageSize = pageSize, HasMoreItems = false };

        var result = await _databaseRepository.GetByUserIdAsync(user.Id, offset, pageSize);

        return new PagedResult<Notification>
        {
            Items = result.Items.Select(e => e.FromPersistence()),
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };
    }

    public async Task<int> GetUnreadCountAsync(UserId userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        if (user == null) return 0;

        return await _databaseRepository.GetUnreadCountAsync(user.Id);
    }

    public async Task AddAsync(Notification notification)
    {
        var entity = notification.ToPersistence();

        var recipientUser = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == notification.RecipientUserId.Value);
        if (recipientUser == null)
            throw new InvalidOperationException($"User with PublicId '{notification.RecipientUserId}' not found");

        entity.RecipientUserId = recipientUser.Id;

        if (notification.SourcePostId != null)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.PublicId == notification.SourcePostId.Value);
            entity.SourcePostId = post?.Id;
        }

        if (notification.SourceDiscussionId != null)
        {
            var discussion = await _context.Discussions.FirstOrDefaultAsync(d => d.PublicId == notification.SourceDiscussionId.Value);
            entity.SourceDiscussionId = discussion?.Id;
        }

        if (notification.SourceSpaceId != null)
        {
            var space = await _context.Spaces.FirstOrDefaultAsync(s => s.PublicId == notification.SourceSpaceId.Value);
            entity.SourceSpaceId = space?.Id;
        }

        if (notification.ActorUserId != null)
        {
            var actorUser = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == notification.ActorUserId.Value);
            entity.ActorUserId = actorUser?.Id;
        }

        await _databaseRepository.AddAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Notification notification)
    {
        var entity = await _context.Notifications.FirstOrDefaultAsync(n => n.PublicId == notification.PublicId.Value);
        if (entity == null)
            throw new InvalidOperationException($"Notification with PublicId '{notification.PublicId}' not found");

        entity.IsRead = notification.IsRead;
        entity.ReadAt = notification.ReadAt;

        await _databaseRepository.UpdateAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(UserId userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        if (user == null) return;

        await _databaseRepository.MarkAllAsReadAsync(user.Id);
    }
}
