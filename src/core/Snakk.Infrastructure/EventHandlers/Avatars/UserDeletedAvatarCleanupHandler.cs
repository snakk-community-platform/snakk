namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class UserDeletedAvatarCleanupHandler : IDomainEventHandler<UserDeletedEvent>
{
    private readonly IAvatarGenerationService _avatarService;
    private readonly ILogger<UserDeletedAvatarCleanupHandler> _logger;

    public UserDeletedAvatarCleanupHandler(
        IAvatarGenerationService avatarService,
        ILogger<UserDeletedAvatarCleanupHandler> logger)
    {
        _avatarService = avatarService;
        _logger = logger;
    }

    public async Task HandleAsync(UserDeletedEvent @event)
    {
        try
        {
            await _avatarService.DeleteAvatarAsync("user", @event.UserId.Value);
            _logger.LogInformation("Deleted avatar for user {UserId}", @event.UserId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete avatar for user {UserId}", @event.UserId.Value);
        }
    }
}
