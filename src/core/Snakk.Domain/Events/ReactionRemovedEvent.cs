namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record ReactionRemovedEvent(
    ReactionId ReactionId,
    PostId PostId,
    UserId UserId,
    ReactionType Type) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
