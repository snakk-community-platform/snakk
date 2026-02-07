namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class CommunityDeletedAvatarCleanupHandler : IDomainEventHandler<CommunityDeletedEvent>
{
    private readonly IAvatarGenerationService _avatarService;
    private readonly ILogger<CommunityDeletedAvatarCleanupHandler> _logger;

    public CommunityDeletedAvatarCleanupHandler(
        IAvatarGenerationService avatarService,
        ILogger<CommunityDeletedAvatarCleanupHandler> logger)
    {
        _avatarService = avatarService;
        _logger = logger;
    }

    public async Task HandleAsync(CommunityDeletedEvent @event)
    {
        try
        {
            await _avatarService.DeleteAvatarAsync("community", @event.CommunityId.Value);
            _logger.LogInformation("Deleted avatar for community {CommunityId}", @event.CommunityId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete avatar for community {CommunityId}", @event.CommunityId.Value);
        }
    }
}
