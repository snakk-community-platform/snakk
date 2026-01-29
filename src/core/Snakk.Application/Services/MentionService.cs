namespace Snakk.Application.Services;

using System.Text.RegularExpressions;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Application.UseCases;

public partial class MentionService(
    IMentionRepository mentionRepository,
    IUserRepository userRepository,
    IDiscussionRepository discussionRepository,
    NotificationUseCase notificationUseCase)
{
    private readonly IMentionRepository _mentionRepository = mentionRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IDiscussionRepository _discussionRepository = discussionRepository;
    private readonly NotificationUseCase _notificationUseCase = notificationUseCase;

    [GeneratedRegex(@"@(\w+)", RegexOptions.Compiled)]
    private static partial Regex MentionRegex();

    /// <summary>
    /// Extract @username mentions from content
    /// </summary>
    public static List<string> ExtractMentionsFromContent(string content)
    {
        var matches = MentionRegex().Matches(content);
        return matches.Select(m => m.Groups[1].Value).Distinct().ToList();
    }

    /// <summary>
    /// Process mentions in a post - create Mention entities and notifications
    /// </summary>
    public async Task ProcessMentionsAsync(
        PostId postId,
        UserId authorUserId,
        string content,
        DiscussionId discussionId)
    {
        var usernames = ExtractMentionsFromContent(content);
        if (usernames.Count == 0) return;

        var discussion = await _discussionRepository.GetByPublicIdAsync(discussionId);
        if (discussion == null) return;

        var author = await _userRepository.GetByPublicIdAsync(authorUserId);
        if (author == null) return;

        var mentions = new List<Mention>();

        foreach (var username in usernames)
        {
            // Find user by display name (case-insensitive)
            var mentionedUser = await _userRepository.GetByDisplayNameAsync(username);
            if (mentionedUser == null) continue;

            // Don't notify yourself
            if (mentionedUser.PublicId.Value == authorUserId.Value) continue;

            // Create mention entity
            var mention = Mention.Create(postId, mentionedUser.PublicId);
            mentions.Add(mention);

            // Create notification
            var notification = Notification.CreateForMention(
                mentionedUser.PublicId,
                authorUserId,
                postId,
                discussionId,
                author.DisplayName,
                discussion.Title);

            await _notificationUseCase.CreateNotificationAsync(notification);
        }

        if (mentions.Count > 0)
        {
            await _mentionRepository.AddRangeAsync(mentions);
        }
    }
}
