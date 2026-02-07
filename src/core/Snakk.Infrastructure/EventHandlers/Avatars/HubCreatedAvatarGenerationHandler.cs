namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class HubCreatedAvatarGenerationHandler : IDomainEventHandler<HubCreatedEvent>
{
    private readonly IAvatarGenerationService _avatarService;
    private readonly ILogger<HubCreatedAvatarGenerationHandler> _logger;

    public HubCreatedAvatarGenerationHandler(
        IAvatarGenerationService avatarService,
        ILogger<HubCreatedAvatarGenerationHandler> logger)
    {
        _avatarService = avatarService;
        _logger = logger;
    }

    public async Task HandleAsync(HubCreatedEvent @event)
    {
        try
        {
            await _avatarService.GenerateHubAvatarAsync(@event.HubId.Value);
            _logger.LogInformation("Generated avatar for new hub {HubId}", @event.HubId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate avatar for hub {HubId}", @event.HubId.Value);
        }
    }
}
