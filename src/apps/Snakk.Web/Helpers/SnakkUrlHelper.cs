namespace Snakk.Web.Helpers;

using Snakk.Web.Services;
using Snakk.Shared.Helpers;

public static class SnakkUrlHelper
{
    /// <summary>
    /// Returns the /c/{slug} prefix for the current community context.
    /// Empty when single-community mode or custom domain (no prefix needed).
    /// </summary>
    private static string GetCommunityPrefix(ICommunityContext community)
        => $"/c/{community.CommunitySlug}";

    /// <summary>
    /// Returns the /c/{slug} prefix for an explicit community slug.
    /// Used in cross-community listings where items may belong to different communities.
    /// </summary>
    private static string GetCommunityPrefix(string? communitySlug, ICommunityContext context)
        => $"/c/{communitySlug}";

    /// <summary>
    /// Public accessor for the community prefix, used by inline JavaScript.
    /// </summary>
    public static string CommunityPrefix(ICommunityContext community)
        => GetCommunityPrefix(community);

    // ===== Community-aware URL methods =====

    public static string Community(string communitySlug, ICommunityContext context)
        => context.IsMultiCommunityEnabled
            ? $"/c/{communitySlug}"
            : "/";

    public static string Hub(ICommunityContext community, string hubSlug)
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}";

    public static string HubWithOffset(ICommunityContext community, string hubSlug, int offset)
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}?offset={offset}";

    public static string Space(ICommunityContext community, string hubSlug, string spaceSlug)
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}/{spaceSlug}";

    public static string SpaceWithOffset(ICommunityContext community, string hubSlug, string spaceSlug, int offset)
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}/{spaceSlug}?offset={offset}";

    /// <summary>
    /// Builds the {slug}~{base62Id} segment used in discussion URLs.
    /// The publicId (ULID) is compressed to 22-char Base62 for shorter URLs.
    /// </summary>
    public static string DiscussionSlugId(string slug, string publicId)
        => $"{slug}~{UlidBase62.Encode(publicId)}";

    public static string NewDiscussion(ICommunityContext community, string hubSlug, string spaceSlug)
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}/{spaceSlug}/new";

    public static string Discussion(ICommunityContext community, string hubSlug, string spaceSlug, string slugWithId)
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}/{spaceSlug}/{slugWithId}";

    public static string DiscussionWithOffset(ICommunityContext community, string hubSlug, string spaceSlug, string slugWithId, int offset)
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}/{spaceSlug}/{slugWithId}?offset={offset}";

    // ===== Explicit-slug overloads (for cross-community listings) =====
    // Used when rendering items from mixed communities (e.g., frontpage in multi-community mode).

    public static string Hub(string communitySlug, ICommunityContext context, string hubSlug)
        => $"{GetCommunityPrefix(communitySlug, context)}/h/{hubSlug}";

    public static string Space(string communitySlug, ICommunityContext context, string hubSlug, string spaceSlug)
        => $"{GetCommunityPrefix(communitySlug, context)}/h/{hubSlug}/{spaceSlug}";

    public static string Discussion(string communitySlug, ICommunityContext context, string hubSlug, string spaceSlug, string slugWithId)
        => $"{GetCommunityPrefix(communitySlug, context)}/h/{hubSlug}/{spaceSlug}/{slugWithId}";

    // ===== Manage URL methods =====

    public static string ManageCommunity(ICommunityContext community)
        => $"/admin{GetCommunityPrefix(community)}";

    public static string ManageHub(ICommunityContext community, string hubSlug)
        => $"/admin{GetCommunityPrefix(community)}/h/{hubSlug}";

    public static string ManageSpace(ICommunityContext community, string hubSlug, string spaceSlug)
        => $"/admin{GetCommunityPrefix(community)}/h/{hubSlug}/s/{spaceSlug}";

    // ===== User URLs =====

    public static string UserProfile(string publicId)
        => $"/u/{UlidBase62.Encode(publicId)}";

    // ===== Avatar & Asset URLs =====

    public static string HubAvatar(string publicId, int revision = 0)
        => AvatarHelper.GetAvatarUrl(publicId, AvatarEntityType.Hub, revision);

    public static string SpaceAvatar(string publicId, int revision = 0)
        => AvatarHelper.GetAvatarUrl(publicId, AvatarEntityType.Space, revision);

    public static string CommunityAvatar(string publicId, int revision = 0)
        => AvatarHelper.GetAvatarUrl(publicId, AvatarEntityType.Community, revision);

    public static string UserAvatar(string publicId, int revision = 0, string? avatarFileName = null)
        => AvatarHelper.GetAvatarUrl(publicId, AvatarEntityType.User, revision, avatarFileName);

    public static string Css(string filename, bool isVendor = false)
    {
        var extension = filename.EndsWith(".css") ? "" : ".css";
        var folder = isVendor ? "vendor" : "dist";
        return $"/css/{folder}/{filename}{extension}";
    }

    public static string Js(string filename, bool isVendor = false)
    {
        var extension = filename.EndsWith(".js") ? "" : ".js";
        var folder = isVendor ? "vendor" : "dist";
        return $"/js/{folder}/{filename}{extension}";
    }
}
