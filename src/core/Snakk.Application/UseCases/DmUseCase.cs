namespace Snakk.Application.UseCases;

using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Shared.Models;

public class DmUseCase(
    IDmRepository dmRepository,
    IRealtimeNotifier realtimeNotifier)
{
    private const int MaxPageSize = 50;

    public async Task<PagedResult<DmConversationDto>> GetConversationsAsync(
        string callerPublicId, int offset, int pageSize, CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, MaxPageSize);
        return await dmRepository.GetConversationsAsync(callerPublicId, offset, pageSize, ct);
    }

    public async Task<DmConversationDto?> GetConversationAsync(
        string conversationPublicId, string callerPublicId, CancellationToken ct = default)
        => await dmRepository.GetConversationAsync(conversationPublicId, callerPublicId, ct);

    public async Task<PagedResult<DmMessageDto>> GetMessagesAsync(
        string conversationPublicId, string callerPublicId,
        int offset, int pageSize, CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, MaxPageSize);
        return await dmRepository.GetMessagesAsync(conversationPublicId, callerPublicId, offset, pageSize, ct);
    }

    public async Task<DmConversationDto?> GetOrCreateConversationAsync(
        string initiatorPublicId, string recipientPublicId, CancellationToken ct = default)
    {
        if (initiatorPublicId == recipientPublicId) return null;
        return await dmRepository.GetOrCreateConversationAsync(initiatorPublicId, recipientPublicId, ct);
    }

    public async Task<DmMessageDto?> SendMessageAsync(
        string conversationPublicId, string senderPublicId,
        string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var isParticipant = await dmRepository.IsParticipantAsync(conversationPublicId, senderPublicId, ct);
        if (!isParticipant) return null;

        var excerpt = content.Length > 200 ? content[..200] : content;

        var message = await dmRepository.SaveMessageAsync(
            conversationPublicId, senderPublicId, content, excerpt, ct);

        if (message is null) return null;

        var recipientInfo = await dmRepository.GetRecipientInfoAsync(conversationPublicId, senderPublicId, ct);
        if (recipientInfo is not null)
        {
            var unreadCount = await dmRepository.GetUnreadConversationCountAsync(recipientInfo.Value.RecipientPublicId, ct);
            await realtimeNotifier.NotifyDirectMessageCountUpdatedAsync(
                recipientInfo.Value.RecipientPublicId,
                recipientInfo.Value.RecipientIntId,
                unreadCount);
        }

        return message;
    }

    public async Task MarkAsReadAsync(
        string conversationPublicId, string callerPublicId, CancellationToken ct = default)
        => await dmRepository.MarkAsReadAsync(conversationPublicId, callerPublicId, ct);

    public async Task<int> GetUnreadConversationCountAsync(
        string callerPublicId, CancellationToken ct = default)
        => await dmRepository.GetUnreadConversationCountAsync(callerPublicId, ct);

    public async Task<bool> DeleteMessagesAsync(
        string conversationPublicId, string callerPublicId,
        IReadOnlyList<string> messagePublicIds, bool deleteForAll, CancellationToken ct = default)
    {
        if (messagePublicIds.Count == 0) return false;
        var isParticipant = await dmRepository.IsParticipantAsync(conversationPublicId, callerPublicId, ct);
        if (!isParticipant) return false;

        var hardDeletedIds = await dmRepository.DeleteMessagesAsync(
            conversationPublicId, callerPublicId, messagePublicIds, deleteForAll, ct);

        // Only notify the other user about messages that were actually removed from the DB
        // (the caller's own messages). Their messages are just hidden from the caller's view.
        if (hardDeletedIds.Count > 0)
        {
            var recipientInfo = await dmRepository.GetRecipientInfoAsync(conversationPublicId, callerPublicId, ct);
            if (recipientInfo is not null)
            {
                await realtimeNotifier.NotifyDmMessagesDeletedAsync(
                    recipientInfo.Value.RecipientPublicId,
                    recipientInfo.Value.RecipientIntId,
                    conversationPublicId,
                    hardDeletedIds);
            }
        }

        return true;
    }

    public async Task<bool> DeleteConversationAsync(
        string conversationPublicId, string callerPublicId, bool deleteForAll, CancellationToken ct = default)
    {
        var isParticipant = await dmRepository.IsParticipantAsync(conversationPublicId, callerPublicId, ct);
        if (!isParticipant) return false;

        var (success, hardDeletedIds) = await dmRepository.DeleteConversationAsync(
            conversationPublicId, callerPublicId, deleteForAll, ct);

        // Only notify the other user about messages the caller actually owned and hard-deleted
        if (success && hardDeletedIds.Count > 0)
        {
            var recipientInfo = await dmRepository.GetRecipientInfoAsync(conversationPublicId, callerPublicId, ct);
            if (recipientInfo is not null)
            {
                await realtimeNotifier.NotifyDmMessagesDeletedAsync(
                    recipientInfo.Value.RecipientPublicId,
                    recipientInfo.Value.RecipientIntId,
                    conversationPublicId,
                    hardDeletedIds);
            }
        }

        return success;
    }

    public async Task<bool> PinConversationAsync(
        string conversationPublicId, string callerPublicId, bool isPinned, CancellationToken ct = default)
    {
        var isParticipant = await dmRepository.IsParticipantAsync(conversationPublicId, callerPublicId, ct);
        if (!isParticipant) return false;
        return await dmRepository.PinConversationAsync(conversationPublicId, callerPublicId, isPinned, ct);
    }
}

public record DmConversationDto(
    string PublicId,
    DmUserDto OtherUser,
    string? LastMessageExcerpt,
    DateTime LastMessageAt,
    bool IsPinned = false);

public record DmUserDto(
    string PublicId,
    string DisplayName,
    string? AvatarThumbnailFileName);

public record DmMessageDto(
    string PublicId,
    string SenderPublicId,
    string Content,
    DateTime CreatedAt,
    bool IsMine);
