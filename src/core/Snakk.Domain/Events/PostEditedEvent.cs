namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record PostEditedEvent(
    PostId PostId,
    DiscussionId DiscussionId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
