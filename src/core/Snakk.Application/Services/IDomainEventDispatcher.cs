namespace Snakk.Application.Services;

using Snakk.Domain.Events;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}
