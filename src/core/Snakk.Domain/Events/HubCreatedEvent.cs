namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record HubCreatedEvent(HubId HubId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
