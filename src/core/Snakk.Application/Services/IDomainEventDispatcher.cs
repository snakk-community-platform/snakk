namespace Snakk.Application.Services;

using Snakk.Domain.Events;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events);
    Task DispatchAsync(IDomainEvent domainEvent);
}
