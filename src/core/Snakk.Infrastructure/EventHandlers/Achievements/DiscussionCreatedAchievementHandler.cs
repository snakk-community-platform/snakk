namespace Snakk.Infrastructure.EventHandlers.Achievements;

using Snakk.Domain.Events;
using Snakk.Application.Events;

// TODO: achievements not yet implemented — handler body disabled pending rewrite
public class DiscussionCreatedAchievementHandler : IDomainEventHandler<DiscussionCreatedEvent>
{
    public Task HandleAsync(DiscussionCreatedEvent @event, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
