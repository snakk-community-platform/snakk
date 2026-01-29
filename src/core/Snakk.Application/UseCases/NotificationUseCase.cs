namespace Snakk.Application.UseCases;

using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Application.Services;
using Snakk.Shared.Models;

public class NotificationUseCase(
    INotificationRepository notificationRepository,
    IRealtimeNotifier realtimeNotifier)
{
    private readonly INotificationRepository _notificationRepository = notificationRepository;
    private readonly IRealtimeNotifier _realtimeNotifier = realtimeNotifier;

    public async Task<PagedResult<NotificationDto>> GetNotificationsAsync(UserId userId, int offset, int pageSize)
    {
        var result = await _notificationRepository.GetByUserIdAsync(userId, offset, pageSize);

        return new PagedResult<NotificationDto>
        {
            Items = result.Items.Select(n => new NotificationDto(
                n.PublicId,
                n.Type.ToString(),
                n.Title,
                n.Body,
                n.SourcePostId?.Value,
                n.SourceDiscussionId?.Value,
                n.ActorUserId?.Value,
                n.IsRead,
                n.CreatedAt)),
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };
    }

    public async Task<int> GetUnreadCountAsync(UserId userId)
    {
        return await _notificationRepository.GetUnreadCountAsync(userId);
    }

    public async Task<Result> MarkAsReadAsync(NotificationId notificationId, UserId userId)
    {
        var notification = await _notificationRepository.GetByPublicIdAsync(notificationId);
        if (notification == null)
            return Result.Failure("Notification not found");

        if (notification.RecipientUserId.Value != userId.Value)
            return Result.Failure("Cannot mark other user's notification as read");

        notification.MarkAsRead();
        await _notificationRepository.UpdateAsync(notification);

        // Notify client to update badge
        var unreadCount = await _notificationRepository.GetUnreadCountAsync(userId);
        await _realtimeNotifier.NotifyUnreadCountUpdatedAsync(userId, unreadCount);

        return Result.Success();
    }

    public async Task MarkAllAsReadAsync(UserId userId)
    {
        await _notificationRepository.MarkAllAsReadAsync(userId);

        // Notify client to update badge
        await _realtimeNotifier.NotifyUnreadCountUpdatedAsync(userId, 0);
    }

    public async Task CreateNotificationAsync(Notification notification)
    {
        await _notificationRepository.AddAsync(notification);

        // Real-time delivery
        await _realtimeNotifier.NotifyUserAsync(notification.RecipientUserId, new
        {
            type = notification.Type.ToString(),
            title = notification.Title,
            body = notification.Body,
            sourceDiscussionId = notification.SourceDiscussionId?.Value,
            sourcePostId = notification.SourcePostId?.Value,
            createdAt = notification.CreatedAt
        });
    }
}

public record NotificationDto(
    string PublicId,
    string Type,
    string Title,
    string? Body,
    string? SourcePostId,
    string? SourceDiscussionId,
    string? ActorUserId,
    bool IsRead,
    DateTime CreatedAt);
