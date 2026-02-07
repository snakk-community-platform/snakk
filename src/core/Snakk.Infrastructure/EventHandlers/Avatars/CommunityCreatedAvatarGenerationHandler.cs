namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class CommunityCreatedAvatarGenerationHandler : IDomainEventHandler<CommunityCreatedEvent>
{
    private readonly IAvatarGenerationService _avatarService;
    private readonly ILogger<CommunityCreatedAvatarGenerationHandler> _logger;

    public CommunityCreatedAvatarGenerationHandler(
        IAvatarGenerationService avatarService,
        ILogger<CommunityCreatedAvatarGenerationHandler> logger)
    {
        _avatarService = avatarService;
        _logger = logger;
    }

    public async Task HandleAsync(CommunityCreatedEvent @event)
    {
        try
        {
            await _avatarService.GenerateCommunityAvatarAsync(@event.CommunityId.Value);
            _logger.LogInformation("Generated avatar for new community {CommunityId}", @event.CommunityId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate avatar for community {CommunityId}", @event.CommunityId.Value);
        }
    }
}
