namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class UserDeletedAvatarCleanupHandler(
    IAvatarGenerationService avatarService,
    ILogger<UserDeletedAvatarCleanupHandler> logger) : IDomainEventHandler<UserDeletedEvent>
{
    public async Task HandleAsync(UserDeletedEvent @event)
    {
        try
        {
            await avatarService.DeleteAvatarAsync("user", @event.UserId.Value);
            logger.LogInformation("Deleted avatar for user {UserId}", @event.UserId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete avatar for user {UserId}", @event.UserId.Value);
        }
    }
}
