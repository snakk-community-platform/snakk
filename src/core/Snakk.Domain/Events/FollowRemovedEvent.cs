namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record FollowRemovedEvent(
    FollowId FollowId,
    UserId UserId,
    FollowTargetType TargetType,
    string TargetId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
