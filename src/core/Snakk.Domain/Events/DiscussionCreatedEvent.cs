namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record DiscussionCreatedEvent(
    DiscussionId DiscussionId,
    SpaceId SpaceId,
    UserId CreatedByUserId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
