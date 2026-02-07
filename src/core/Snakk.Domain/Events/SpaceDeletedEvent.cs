namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record SpaceDeletedEvent(SpaceId SpaceId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
