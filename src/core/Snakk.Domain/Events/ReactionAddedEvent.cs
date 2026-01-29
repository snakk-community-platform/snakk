namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record ReactionAddedEvent(
    ReactionId ReactionId,
    PostId PostId,
    UserId UserId,
    ReactionType Type) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
