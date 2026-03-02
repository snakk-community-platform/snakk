namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class SpaceCreatedAvatarGenerationHandler(
    IAvatarGenerationService avatarService,
    ILogger<SpaceCreatedAvatarGenerationHandler> logger) : IDomainEventHandler<SpaceCreatedEvent>
{
    public async Task HandleAsync(SpaceCreatedEvent @event)
    {
        try
        {
            await avatarService.GenerateSpaceAvatarAsync(@event.SpaceId.Value);
            logger.LogInformation("Generated avatar for new space {SpaceId}", @event.SpaceId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate avatar for space {SpaceId}", @event.SpaceId.Value);
        }
    }
}
