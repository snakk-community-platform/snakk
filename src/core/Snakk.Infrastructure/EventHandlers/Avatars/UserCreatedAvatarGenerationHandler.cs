namespace Snakk.Infrastructure.EventHandlers.Avatars;

using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;

public class UserCreatedAvatarGenerationHandler : IDomainEventHandler<UserCreatedEvent>
{
    private readonly IAvatarGenerationService _avatarService;
    private readonly ILogger<UserCreatedAvatarGenerationHandler> _logger;

    public UserCreatedAvatarGenerationHandler(
        IAvatarGenerationService avatarService,
        ILogger<UserCreatedAvatarGenerationHandler> logger)
    {
        _avatarService = avatarService;
        _logger = logger;
    }

    public async Task HandleAsync(UserCreatedEvent @event)
    {
        try
        {
            await _avatarService.GenerateUserAvatarAsync(@event.UserId.Value);
            _logger.LogInformation("Generated avatar for new user {UserId}", @event.UserId.Value);
        }
        catch (Exception ex)
        {
            // Non-critical - log but don't throw
            // Avatar can be generated on-demand if this fails
            _logger.LogError(ex, "Failed to generate avatar for user {UserId}", @event.UserId.Value);
        }
    }
}
