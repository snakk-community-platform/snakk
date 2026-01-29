namespace Snakk.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
