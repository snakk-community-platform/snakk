namespace Snakk.Application.Services;

public interface IDiscordNotificationService
{
    Task NotifyDiscussionCreatedAsync(string spacePublicId, string discussionTitle,
        string discussionUrl, string authorDisplayName, string spaceName,
        CancellationToken ct = default);

    Task NotifyPostCreatedAsync(string spacePublicId, string discussionTitle,
        string discussionUrl, string authorDisplayName, string spaceName,
        CancellationToken ct = default);
}
