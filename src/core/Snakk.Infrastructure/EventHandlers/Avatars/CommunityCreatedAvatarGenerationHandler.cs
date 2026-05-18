namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class CommunityCreatedAvatarGenerationHandler(
    IAvatarGenerationService avatarService,
    ILogger<CommunityCreatedAvatarGenerationHandler> logger) : IDomainEventHandler<CommunityCreatedEvent>
{
    public async Task HandleAsync(CommunityCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await avatarService.GenerateCommunityAvatarAsync(@event.CommunityId.Value);
            logger.LogInformation("Generated avatar for new community {CommunityId}", @event.CommunityId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate avatar for community {CommunityId}", @event.CommunityId.Value);
        }
    }
}
