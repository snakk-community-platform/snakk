namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record UserCreatedEvent(UserId UserId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
