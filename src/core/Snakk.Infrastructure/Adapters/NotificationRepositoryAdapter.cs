namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Domain.Extensions;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class NotificationRepositoryAdapter(
    INotificationDatabaseRepository databaseRepository,
    SnakkDbContext context) : INotificationRepository
{
    public async Task<Notification?> GetByPublicIdAsync(NotificationId notificationId)
    {
        var projection = await context.UserNotifications
            .Where(n => n.PublicId == notificationId.Value)
            .Select(n => new NotificationProjection(
                n.PublicId, n.RecipientUserPublicId!, n.TypeId,
                n.Title, n.Body,
                n.SourcePostPublicId,
                n.SourceDiscussionPublicId,
                n.SourceSpacePublicId,
                n.ActorUserPublicId,
                n.IsRead, n.CreatedAt, n.ReadAt))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<PagedResult<Notification>> GetByUserIdAsync(
        UserId userId,
        int offset,
        int pageSize)
    {
        var userDbId = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();

        if (userDbId is null)
            return new PagedResult<Notification> { Items = [], Offset = offset, PageSize = pageSize, HasMoreItems = false };

        var projections = await context.UserNotifications
            .Where(n => n.RecipientUserId == userDbId.Value)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(n => new NotificationProjection(
                n.PublicId, n.RecipientUserPublicId!, n.TypeId,
                n.Title, n.Body,
                n.SourcePostPublicId,
                n.SourceDiscussionPublicId,
                n.SourceSpacePublicId,
                n.ActorUserPublicId,
                n.IsRead, n.CreatedAt, n.ReadAt))
            .ToListAsync();

        var hasMoreItems = projections.Count > pageSize;
        var items = hasMoreItems ? projections.Take(pageSize) : projections;

        return new PagedResult<Notification>
        {
            Items = items.Select(p => p.ToDomain()),
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task<int> GetUnreadCountAsync(UserId userId) =>
        await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Select(u => u.UnreadNotificationCount)
            .FirstOrDefaultAsync();

    public async Task AddAsync(Notification notification)
    {
        var entity = notification.ToPersistence();

        var recipientUser = await context.Users.FirstOrDefaultAsync(u => u.PublicId == notification.RecipientUserId.Value);

        if (recipientUser is null)
            throw new InvalidOperationException($"User with PublicId '{notification.RecipientUserId}' not found");

        entity.RecipientUserId = recipientUser.Id;
        entity.RecipientUserPublicId = notification.RecipientUserId.Value;

        if (notification.SourcePostId is not null)
        {
            var post = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == notification.SourcePostId.Value);
            entity.SourcePostId = post?.Id;
            entity.SourcePostPublicId = notification.SourcePostId.Value;
        }

        if (notification.SourceDiscussionId is not null)
        {
            var discussion = await context.Discussions.FirstOrDefaultAsync(d => d.PublicId == notification.SourceDiscussionId.Value);
            entity.SourceDiscussionId = discussion?.Id;
            entity.SourceDiscussionPublicId = notification.SourceDiscussionId.Value;
        }

        if (notification.SourceSpaceId is not null)
        {
            var space = await context.Spaces.FirstOrDefaultAsync(s => s.PublicId == notification.SourceSpaceId.Value);
            entity.SourceSpaceId = space?.Id;
            entity.SourceSpacePublicId = notification.SourceSpaceId.Value;
        }

        if (notification.ActorUserId is not null)
        {
            var actorUser = await context.Users.FirstOrDefaultAsync(u => u.PublicId == notification.ActorUserId.Value);
            entity.ActorUserId = actorUser?.Id;
            entity.ActorUserPublicId = notification.ActorUserId.Value;
        }

        await databaseRepository.AddAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Notification notification)
    {
        var entity = await context.UserNotifications.FirstOrDefaultAsync(n => n.PublicId == notification.PublicId.Value);

        if (entity is null)
            throw new InvalidOperationException($"Notification with PublicId '{notification.PublicId}' not found");

        entity.IsRead = notification.IsRead;
        entity.ReadAt = notification.ReadAt;

        await databaseRepository.UpdateAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(UserId userId)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user is null) return;

        await databaseRepository.MarkAllAsReadAsync(user.Id);
    }

    private record NotificationProjection(
        string PublicId,
        string RecipientUserPublicId,
        int TypeId,
        string Title,
        string? Body,
        string? SourcePostPublicId,
        string? SourceDiscussionPublicId,
        string? SourceSpacePublicId,
        string? ActorUserPublicId,
        bool IsRead,
        DateTime CreatedAt,
        DateTime? ReadAt)
    {
        public Notification ToDomain() => Notification.Rehydrate(
            NotificationId.From(PublicId),
            UserId.From(RecipientUserPublicId),
            ((NotificationTypeEnum)TypeId).ToDomain(),
            Title, Body,
            SourcePostPublicId is not null ? PostId.From(SourcePostPublicId) : null,
            SourceDiscussionPublicId is not null ? DiscussionId.From(SourceDiscussionPublicId) : null,
            SourceSpacePublicId is not null ? SpaceId.From(SourceSpacePublicId) : null,
            ActorUserPublicId is not null ? UserId.From(ActorUserPublicId) : null,
            IsRead, CreatedAt, ReadAt);
    }
}
