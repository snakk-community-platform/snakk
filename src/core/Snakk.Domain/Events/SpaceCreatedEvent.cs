namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record SpaceCreatedEvent(SpaceId SpaceId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
