using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Snakk.Application.Events;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class DomainEventDispatcherTests
{
    #region Test Event and Handler

    private record TestDomainEvent(string Message) : IDomainEvent
    {
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }

    private record AnotherDomainEvent(int Value) : IDomainEvent
    {
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }

    private class TestEventHandler : IDomainEventHandler<TestDomainEvent>
    {
        public bool WasCalled { get; private set; }
        public TestDomainEvent? ReceivedEvent { get; private set; }

        public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            ReceivedEvent = domainEvent;

            return Task.CompletedTask;
        }
    }

    private class SecondTestEventHandler : IDomainEventHandler<TestDomainEvent>
    {
        public bool WasCalled { get; private set; }

        public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private class AnotherEventHandler : IDomainEventHandler<AnotherDomainEvent>
    {
        public bool WasCalled { get; private set; }

        public Task HandleAsync(AnotherDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    #endregion

    #region DispatchAsync (single event) Tests

    [Test]
    public async Task DispatchAsync_RoutesEventToCorrectHandler()
    {
        // Arrange
        var handler = new TestEventHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<TestDomainEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(provider);
        var @event = new TestDomainEvent("Hello");

        // Act
        await dispatcher.DispatchAsync(@event);

        // Assert
        await Assert.That(handler.WasCalled).IsTrue();
        await Assert.That(handler.ReceivedEvent).IsNotNull();
        await Assert.That(handler.ReceivedEvent!.Message).IsEqualTo("Hello");
    }

    [Test]
    public async Task DispatchAsync_CallsMultipleHandlers_ForSameEvent()
    {
        // Arrange
        var handler1 = new TestEventHandler();
        var handler2 = new SecondTestEventHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<TestDomainEvent>>(handler1);
        services.AddSingleton<IDomainEventHandler<TestDomainEvent>>(handler2);
        var provider = services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(provider);

        // Act
        await dispatcher.DispatchAsync(new TestDomainEvent("Test"));

        // Assert
        await Assert.That(handler1.WasCalled).IsTrue();
        await Assert.That(handler2.WasCalled).IsTrue();
    }

    [Test]
    public async Task DispatchAsync_DoesNotCallUnrelatedHandlers()
    {
        // Arrange
        var testHandler = new TestEventHandler();
        var anotherHandler = new AnotherEventHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<TestDomainEvent>>(testHandler);
        services.AddSingleton<IDomainEventHandler<AnotherDomainEvent>>(anotherHandler);
        var provider = services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(provider);

        // Act
        await dispatcher.DispatchAsync(new TestDomainEvent("Test"));

        // Assert
        await Assert.That(testHandler.WasCalled).IsTrue();
        await Assert.That(anotherHandler.WasCalled).IsFalse();
    }

    [Test]
    public async Task DispatchAsync_WithNoHandlers_CompletesSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(provider);

        // Act & Assert
        var act = async () => await dispatcher.DispatchAsync(new TestDomainEvent("NoHandler"));
        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region DispatchAsync (multiple events) Tests

    [Test]
    public async Task DispatchAsync_MultipleEvents_DispatchesAll()
    {
        // Arrange
        var testHandler = new TestEventHandler();
        var anotherHandler = new AnotherEventHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<TestDomainEvent>>(testHandler);
        services.AddSingleton<IDomainEventHandler<AnotherDomainEvent>>(anotherHandler);
        var provider = services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(provider);

        var events = new IDomainEvent[]
        {
            new TestDomainEvent("First"),
            new AnotherDomainEvent(42)
        };

        // Act
        await dispatcher.DispatchAsync(events);

        // Assert
        await Assert.That(testHandler.WasCalled).IsTrue();
        await Assert.That(anotherHandler.WasCalled).IsTrue();
    }

    [Test]
    public async Task DispatchAsync_EmptyEventList_CompletesSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(provider);

        // Act & Assert
        var act = async () => await dispatcher.DispatchAsync([]);
        await Assert.That(act).ThrowsNothing();
    }

    #endregion
}
