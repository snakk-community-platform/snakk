namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class CommunityDeletedAvatarCleanupHandler(
    IAvatarGenerationService avatarService,
    ILogger<CommunityDeletedAvatarCleanupHandler> logger) : IDomainEventHandler<CommunityDeletedEvent>
{
    public async Task HandleAsync(CommunityDeletedEvent @event)
    {
        try
        {
            await avatarService.DeleteAvatarAsync("community", @event.CommunityId.Value);
            logger.LogInformation("Deleted avatar for community {CommunityId}", @event.CommunityId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete avatar for community {CommunityId}", @event.CommunityId.Value);
        }
    }
}
