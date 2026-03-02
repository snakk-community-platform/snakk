using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Api.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Protos.Notification;

namespace Snakk.Api.GrpcServices;

public class NotificationGrpcService(
    NotificationUseCase notificationUseCase,
    ICurrentUserService currentUser) : NotificationService.NotificationServiceBase
{
    public override async Task<PagedNotificationList> GetNotifications(GetNotificationsRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var result = await notificationUseCase.GetNotificationsAsync(userId, request.Offset, request.PageSize);

        var response = new PagedNotificationList
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var n in result.Items)
        {
            var item = new NotificationInfo
            {
                PublicId = n.PublicId,
                Type = n.Type,
                Message = n.Title,
                IsRead = n.IsRead,
                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc))
            };

            if (n.ActorUserId is not null)
            {
                item.SourcePublicId = n.ActorUserId;
                item.SourceType = "User";
            }

            if (n.SourceDiscussionId is not null)
            {
                item.TargetPublicId = n.SourceDiscussionId;
                item.TargetType = "Discussion";
            }
            else if (n.SourcePostId is not null)
            {
                item.TargetPublicId = n.SourcePostId;
                item.TargetType = "Post";
            }

            if (n.Body is not null)
                item.SourceDisplayName = n.Body;

            response.Items.Add(item);
        }

        return response;
    }

    public override async Task<UnreadCountResponse> GetUnreadCount(GetUnreadCountRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var count = await notificationUseCase.GetUnreadCountAsync(userId);

        return new UnreadCountResponse { Count = count };
    }

    public override async Task<MarkAsReadResponse> MarkAsRead(Snakk.Protos.Notification.MarkAsReadRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var result = await notificationUseCase.MarkAsReadAsync(
            NotificationId.From(request.NotificationId),
            userId);

        return new MarkAsReadResponse { Success = result.IsSuccess };
    }

    public override async Task<MarkAllAsReadResponse> MarkAllAsRead(MarkAllAsReadRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        await notificationUseCase.MarkAllAsReadAsync(userId);

        return new MarkAllAsReadResponse { Success = true };
    }

    private UserId RequireAuth()
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userId = currentUser.GetCurrentUserId();

        if (userId is null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        return UserId.From(userId);
    }
}
