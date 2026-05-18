namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class UserCreatedAvatarGenerationHandler(
    IAvatarGenerationService avatarService,
    ILogger<UserCreatedAvatarGenerationHandler> logger) : IDomainEventHandler<UserCreatedEvent>
{
    public async Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await avatarService.GenerateUserAvatarAsync(@event.UserId.Value);
            logger.LogInformation("Generated avatar for new user {UserId}", @event.UserId.Value);
        }
        catch (Exception ex)
        {
            // Non-critical - log but don't throw
            // Avatar can be generated on-demand if this fails
            logger.LogError(ex, "Failed to generate avatar for user {UserId}", @event.UserId.Value);
        }
    }
}
