namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record MentionCreatedEvent(
    MentionId MentionId,
    PostId PostId,
    UserId MentionedUserId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
