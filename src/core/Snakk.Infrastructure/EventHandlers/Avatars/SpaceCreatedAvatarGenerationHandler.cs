namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class SpaceCreatedAvatarGenerationHandler : IDomainEventHandler<SpaceCreatedEvent>
{
    private readonly IAvatarGenerationService _avatarService;
    private readonly ILogger<SpaceCreatedAvatarGenerationHandler> _logger;

    public SpaceCreatedAvatarGenerationHandler(
        IAvatarGenerationService avatarService,
        ILogger<SpaceCreatedAvatarGenerationHandler> logger)
    {
        _avatarService = avatarService;
        _logger = logger;
    }

    public async Task HandleAsync(SpaceCreatedEvent @event)
    {
        try
        {
            await _avatarService.GenerateSpaceAvatarAsync(@event.SpaceId.Value);
            _logger.LogInformation("Generated avatar for new space {SpaceId}", @event.SpaceId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate avatar for space {SpaceId}", @event.SpaceId.Value);
        }
    }
}
