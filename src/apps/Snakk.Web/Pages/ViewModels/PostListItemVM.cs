using Snakk.Protos.Save;
using Snakk.Protos.Search;
using Snakk.Web.Helpers;

namespace Snakk.Web.Pages.ViewModels;

public record PostListItemVM
{
    public required string PublicId { get; init; }
    public required string ContentExcerpt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required string AuthorPublicId { get; init; }
    public required string AuthorDisplayName { get; init; }
    public required string AuthorAvatarUrl { get; init; }
    public required string DiscussionPublicId { get; init; }
    public required string DiscussionTitle { get; init; }
    public required string DiscussionSlug { get; init; }
    public required string SpaceSlug { get; init; }
    public required string HubSlug { get; init; }
    public required string CommunitySlug { get; init; }
    public string HubName { get; init; } = "";
    public string SpaceName { get; init; } = "";
    public string SpacePublicId { get; init; } = "";

    public static PostListItemVM FromSavedPost(SavedPostInfo p) => new()
    {
        PublicId = p.PublicId,
        ContentExcerpt = p.ContentExcerpt,
        CreatedAt = p.CreatedAt.ToDateTime(),
        AuthorPublicId = p.AuthorPublicId,
        AuthorDisplayName = p.AuthorDisplayName,
        AuthorAvatarUrl = SnakkUrlHelper.UserAvatar(p.AuthorPublicId,
            avatarFileName: p.HasAuthorAvatarFileName ? p.AuthorAvatarFileName : null),
        DiscussionPublicId = p.DiscussionPublicId,
        DiscussionTitle = p.DiscussionTitle,
        DiscussionSlug = p.DiscussionSlug,
        SpaceSlug = p.SpaceSlug,
        HubSlug = p.HubSlug,
        CommunitySlug = p.CommunitySlug,
        HubName = p.HubName,
        SpaceName = p.SpaceName,
    };

    public static PostListItemVM FromSearchResult(PostSearchResult p) => new()
    {
        PublicId = p.PublicId,
        ContentExcerpt = p.ContentHighlight,
        CreatedAt = p.CreatedAt.ToDateTime(),
        AuthorPublicId = p.Author.PublicId,
        AuthorDisplayName = p.Author.DisplayName,
        AuthorAvatarUrl = p.Author.HasAvatarUrl
            ? p.Author.AvatarUrl
            : SnakkUrlHelper.UserAvatar(p.Author.PublicId),
        DiscussionPublicId = p.DiscussionPublicId,
        DiscussionTitle = p.DiscussionTitle,
        DiscussionSlug = p.DiscussionSlug,
        SpaceSlug = p.Space?.Slug ?? "",
        HubSlug = p.Hub?.Slug ?? "",
        CommunitySlug = p.CommunitySlug,
        HubName = p.Hub?.Name ?? "",
        SpaceName = p.Space?.Name ?? "",
        SpacePublicId = p.Space?.PublicId ?? "",
    };
}
