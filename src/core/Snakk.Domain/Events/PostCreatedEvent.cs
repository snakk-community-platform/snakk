namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record PostCreatedEvent(
    PostId PostId,
    DiscussionId DiscussionId,
    UserId CreatedByUserId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
