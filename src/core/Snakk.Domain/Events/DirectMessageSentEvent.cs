namespace Snakk.Domain.Events;

public record DirectMessageSentEvent(
    string ConversationPublicId,
    string RecipientUserPublicId,
    int RecipientUserId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
