namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class HubCreatedAvatarGenerationHandler(
    IAvatarGenerationService avatarService,
    ILogger<HubCreatedAvatarGenerationHandler> logger) : IDomainEventHandler<HubCreatedEvent>
{
    public async Task HandleAsync(HubCreatedEvent @event)
    {
        try
        {
            await avatarService.GenerateHubAvatarAsync(@event.HubId.Value);
            logger.LogInformation("Generated avatar for new hub {HubId}", @event.HubId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate avatar for hub {HubId}", @event.HubId.Value);
        }
    }
}
