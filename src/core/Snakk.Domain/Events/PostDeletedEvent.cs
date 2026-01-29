namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record PostDeletedEvent(
    PostId PostId,
    DiscussionId DiscussionId,
    UserId DeletedByUserId,
    bool IsHardDelete) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
