namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class SpaceDeletedAvatarCleanupHandler(
    IAvatarGenerationService avatarService,
    ILogger<SpaceDeletedAvatarCleanupHandler> logger) : IDomainEventHandler<SpaceDeletedEvent>
{
    public async Task HandleAsync(SpaceDeletedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await avatarService.DeleteAvatarAsync("space", @event.SpaceId.Value);
            logger.LogInformation("Deleted avatar for space {SpaceId}", @event.SpaceId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete avatar for space {SpaceId}", @event.SpaceId.Value);
        }
    }
}
