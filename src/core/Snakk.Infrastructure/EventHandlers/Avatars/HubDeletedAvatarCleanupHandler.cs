namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class HubDeletedAvatarCleanupHandler : IDomainEventHandler<HubDeletedEvent>
{
    private readonly IAvatarGenerationService _avatarService;
    private readonly ILogger<HubDeletedAvatarCleanupHandler> _logger;

    public HubDeletedAvatarCleanupHandler(
        IAvatarGenerationService avatarService,
        ILogger<HubDeletedAvatarCleanupHandler> logger)
    {
        _avatarService = avatarService;
        _logger = logger;
    }

    public async Task HandleAsync(HubDeletedEvent @event)
    {
        try
        {
            await _avatarService.DeleteAvatarAsync("hub", @event.HubId.Value);
            _logger.LogInformation("Deleted avatar for hub {HubId}", @event.HubId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete avatar for hub {HubId}", @event.HubId.Value);
        }
    }
}
