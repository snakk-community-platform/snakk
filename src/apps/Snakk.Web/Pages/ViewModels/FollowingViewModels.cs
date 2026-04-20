namespace Snakk.Web.Pages.ViewModels;

public record FollowingSpaceVM
{
    public required string PublicId { get; init; }
    public required string Name { get; init; }
    public required string Href { get; init; }
    public required string AvatarUrl { get; init; }
    public string? Description { get; init; }
    public int DiscussionCount { get; init; }
    public int ReplyCount { get; init; }
    public FollowingSpaceLatestDiscussionVM? LatestDiscussion { get; init; }
}

public record FollowingSpaceLatestDiscussionVM
{
    public required string Href { get; init; }
    public required string Title { get; init; }
    public required string AuthorDisplayName { get; init; }
    public required string AuthorAvatarUrl { get; init; }
    public DateTime? LastActivityAt { get; init; }
}

public record FollowingDiscussionVM
{
    public required string PublicId { get; init; }
    public required string Title { get; init; }
    public required string Href { get; init; }
    public required string SpacePublicId { get; init; }
    public required string SpaceName { get; init; }
    public required string SpaceHref { get; init; }
    public int ReplyCount { get; init; }
}

public record FollowingUserVM
{
    public required string PublicId { get; init; }
    public required string DisplayName { get; init; }
    public required string Href { get; init; }
    public required string AvatarUrl { get; init; }
    public int FollowerCount { get; init; }
}
