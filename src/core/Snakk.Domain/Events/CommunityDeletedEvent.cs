namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record CommunityDeletedEvent(CommunityId CommunityId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
