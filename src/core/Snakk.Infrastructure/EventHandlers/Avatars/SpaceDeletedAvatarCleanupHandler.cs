namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class SpaceDeletedAvatarCleanupHandler : IDomainEventHandler<SpaceDeletedEvent>
{
    private readonly IAvatarGenerationService _avatarService;
    private readonly ILogger<SpaceDeletedAvatarCleanupHandler> _logger;

    public SpaceDeletedAvatarCleanupHandler(
        IAvatarGenerationService avatarService,
        ILogger<SpaceDeletedAvatarCleanupHandler> logger)
    {
        _avatarService = avatarService;
        _logger = logger;
    }

    public async Task HandleAsync(SpaceDeletedEvent @event)
    {
        try
        {
            await _avatarService.DeleteAvatarAsync("space", @event.SpaceId.Value);
            _logger.LogInformation("Deleted avatar for space {SpaceId}", @event.SpaceId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete avatar for space {SpaceId}", @event.SpaceId.Value);
        }
    }
}
