namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record HubDeletedEvent(HubId HubId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
