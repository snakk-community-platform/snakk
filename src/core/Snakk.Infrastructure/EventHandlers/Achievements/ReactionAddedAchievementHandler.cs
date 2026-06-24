namespace Snakk.Infrastructure.EventHandlers.Achievements;

using Snakk.Domain.Events;
using Snakk.Application.Events;

// TODO: achievements not yet implemented — handler body disabled pending rewrite
public class ReactionAddedAchievementHandler : IDomainEventHandler<ReactionAddedEvent>
{
    public Task HandleAsync(ReactionAddedEvent @event, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
