namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record CommunityCreatedEvent(CommunityId CommunityId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
