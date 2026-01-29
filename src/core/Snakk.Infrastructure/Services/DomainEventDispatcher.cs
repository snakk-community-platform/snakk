namespace Snakk.Infrastructure.Services;

using Snakk.Application.Services;
using Snakk.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Snakk.Application.Events;

public class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events)
    {
        foreach (var domainEvent in events)
        {
            await DispatchAsync(domainEvent);
        }
    }

    public async Task DispatchAsync(IDomainEvent domainEvent)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        var handlers = _serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            var handleMethod = handlerType.GetMethod("HandleAsync");
            if (handleMethod != null)
            {
                var task = (Task?)handleMethod.Invoke(handler, [domainEvent]);
                if (task != null)
                {
                    await task;
                }
            }
        }
    }
}
