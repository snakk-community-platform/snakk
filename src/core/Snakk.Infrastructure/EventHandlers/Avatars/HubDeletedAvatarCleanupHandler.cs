namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class HubDeletedAvatarCleanupHandler(
    IAvatarGenerationService avatarService,
    ILogger<HubDeletedAvatarCleanupHandler> logger) : IDomainEventHandler<HubDeletedEvent>
{
    public async Task HandleAsync(HubDeletedEvent @event)
    {
        try
        {
            await avatarService.DeleteAvatarAsync("hub", @event.HubId.Value);
            logger.LogInformation("Deleted avatar for hub {HubId}", @event.HubId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete avatar for hub {HubId}", @event.HubId.Value);
        }
    }
}
