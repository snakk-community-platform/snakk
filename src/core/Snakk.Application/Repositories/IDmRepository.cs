namespace Snakk.Application.Repositories;

using Snakk.Application.UseCases;
using Snakk.Shared.Models;

public interface IDmRepository
{
    Task<PagedResult<DmConversationDto>> GetConversationsAsync(
        string callerPublicId, int offset, int pageSize, CancellationToken ct = default);

    Task<DmConversationDto?> GetConversationAsync(
        string conversationPublicId, string callerPublicId, CancellationToken ct = default);

    Task<bool> IsParticipantAsync(
        string conversationPublicId, string callerPublicId, CancellationToken ct = default);

    Task<PagedResult<DmMessageDto>> GetMessagesAsync(
        string conversationPublicId, string callerPublicId,
        int offset, int pageSize, CancellationToken ct = default);

    Task<DmConversationDto?> GetOrCreateConversationAsync(
        string initiatorPublicId, string recipientPublicId, CancellationToken ct = default);

    Task<DmMessageDto?> SaveMessageAsync(
        string conversationPublicId, string senderPublicId,
        string content, string excerpt, CancellationToken ct = default);

    Task MarkAsReadAsync(
        string conversationPublicId, string callerPublicId, CancellationToken ct = default);

    Task<int> GetUnreadConversationCountAsync(
        string callerPublicId, CancellationToken ct = default);

    Task<(string RecipientPublicId, int RecipientIntId)?> GetRecipientInfoAsync(
        string conversationPublicId, string senderPublicId, CancellationToken ct = default);

    Task<int> GetUserIntIdAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Returns the public IDs that were hard-deleted (only the caller's own messages).
    /// Other-user messages are hidden for the caller instead.
    /// </summary>
    Task<IReadOnlyList<string>> DeleteMessagesAsync(
        string conversationPublicId, string callerPublicId,
        IReadOnlyList<string> messagePublicIds, bool deleteForAll, CancellationToken ct = default);

    /// <summary>
    /// Returns the public IDs of the caller's messages that were hard-deleted.
    /// The other user's messages are hidden from the caller's view.
    /// </summary>
    Task<(bool Success, IReadOnlyList<string> HardDeletedIds)> DeleteConversationAsync(
        string conversationPublicId, string callerPublicId, bool deleteForAll, CancellationToken ct = default);

    Task<bool> PinConversationAsync(
        string conversationPublicId, string callerPublicId, bool isPinned, CancellationToken ct = default);
}
