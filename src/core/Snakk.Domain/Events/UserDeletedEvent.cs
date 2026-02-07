namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record UserDeletedEvent(UserId UserId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
