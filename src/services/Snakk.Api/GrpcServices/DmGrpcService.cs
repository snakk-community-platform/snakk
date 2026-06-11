using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Api.Services;
using Snakk.Application.UseCases;
using Snakk.Protos.Dm;

namespace Snakk.Api.GrpcServices;

public class DmGrpcService(
    DmUseCase dmUseCase,
    ICurrentUserService currentUser) : DmService.DmServiceBase
{
    public override async Task<PagedConversationList> GetConversations(
        GetConversationsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var callerId = TryGetCallerId();
        if (callerId is null) return new PagedConversationList();

        var result = await dmUseCase.GetConversationsAsync(callerId, request.Offset, request.PageSize, ct);

        var response = new PagedConversationList
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var c in result.Items)
            response.Items.Add(ToConversationInfo(c));

        return response;
    }

    public override async Task<ConversationResponse> GetConversation(
        GetConversationRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var callerId = TryGetCallerId();
        if (callerId is null) return new ConversationResponse { Found = false };

        var conv = await dmUseCase.GetConversationAsync(request.ConversationId, callerId, ct);
        if (conv is null) return new ConversationResponse { Found = false };

        return new ConversationResponse { Found = true, Conversation = ToConversationInfo(conv) };
    }

    public override async Task<ConversationResponse> GetOrCreateConversation(
        GetOrCreateConversationRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var callerId = RequireAuth();

        var conv = await dmUseCase.GetOrCreateConversationAsync(callerId, request.RecipientPublicId, ct);
        if (conv is null) return new ConversationResponse { Found = false };

        return new ConversationResponse { Found = true, Conversation = ToConversationInfo(conv) };
    }

    public override async Task<PagedMessageList> GetMessages(
        GetMessagesRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var callerId = TryGetCallerId();
        if (callerId is null) return new PagedMessageList();

        var result = await dmUseCase.GetMessagesAsync(
            request.ConversationId, callerId, request.Offset, request.PageSize, ct);

        var response = new PagedMessageList
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var m in result.Items)
            response.Items.Add(ToMessageInfo(m));

        return response;
    }

    public override async Task<SendMessageResponse> SendMessage(
        SendMessageRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var callerId = RequireAuth();

        var message = await dmUseCase.SendMessageAsync(
            request.ConversationId, callerId, request.Content, ct);

        if (message is null) return new SendMessageResponse { Success = false };

        return new SendMessageResponse { Success = true, Message = ToMessageInfo(message) };
    }

    public override async Task<MarkAsReadResponse> MarkAsRead(
        MarkAsReadRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var callerId = RequireAuth();

        await dmUseCase.MarkAsReadAsync(request.ConversationId, callerId, ct);
        return new MarkAsReadResponse { Success = true };
    }

    public override async Task<UnreadCountResponse> GetUnreadCount(
        GetUnreadCountRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var callerId = TryGetCallerId();
        if (callerId is null) return new UnreadCountResponse { Count = 0 };

        var count = await dmUseCase.GetUnreadConversationCountAsync(callerId, ct);
        return new UnreadCountResponse { Count = count };
    }

    public override async Task<DeleteMessagesResponse> DeleteMessages(
        DeleteMessagesRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var callerId = RequireAuth();

        var success = await dmUseCase.DeleteMessagesAsync(
            request.ConversationId, callerId, request.MessageIds.ToList(), request.DeleteForAll, ct);

        return new DeleteMessagesResponse { Success = success };
    }

    public override async Task<DeleteConversationResponse> DeleteConversation(
        DeleteConversationRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var callerId = RequireAuth();

        var success = await dmUseCase.DeleteConversationAsync(request.ConversationId, callerId, request.DeleteForAll, ct);
        return new DeleteConversationResponse { Success = success };
    }

    public override async Task<PinConversationResponse> PinConversation(
        PinConversationRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var callerId = RequireAuth();

        var success = await dmUseCase.PinConversationAsync(request.ConversationId, callerId, request.IsPinned, ct);
        return new PinConversationResponse { Success = success };
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static ConversationInfo ToConversationInfo(DmConversationDto c)
    {
        var info = new ConversationInfo
        {
            PublicId = c.PublicId,
            OtherUser = new DmUserInfo
            {
                PublicId = c.OtherUser.PublicId,
                DisplayName = c.OtherUser.DisplayName,
            },
            LastMessageExcerpt = c.LastMessageExcerpt ?? "",
            LastMessageAt = Timestamp.FromDateTime(DateTime.SpecifyKind(c.LastMessageAt, DateTimeKind.Utc)),
            IsPinned = c.IsPinned,
        };

        if (c.OtherUser.AvatarThumbnailFileName is not null)
            info.OtherUser.AvatarThumbnailFileName = c.OtherUser.AvatarThumbnailFileName;

        return info;
    }

    private static MessageInfo ToMessageInfo(DmMessageDto m) => new()
    {
        PublicId = m.PublicId,
        SenderPublicId = m.SenderPublicId,
        Content = m.Content,
        CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(m.CreatedAt, DateTimeKind.Utc)),
        IsMine = m.IsMine
    };

    private string? TryGetCallerId()
    {
        if (!currentUser.IsAuthenticated()) return null;
        return currentUser.GetCurrentUserId();
    }

    private string RequireAuth() =>
        TryGetCallerId()
        ?? throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));
}
