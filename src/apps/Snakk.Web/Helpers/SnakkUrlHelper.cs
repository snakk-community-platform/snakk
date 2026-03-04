namespace Snakk.Web.Helpers;

using Snakk.Web.Services;
using Snakk.Shared.Helpers;

public static class SnakkUrlHelper
{
    /// <summary>
    /// Gets the URL prefix for the current community context.
    /// - Empty for the default community (its content lives at /h/... directly)
    /// - Empty for custom domains (domain itself identifies the community)
    /// - /c/{slug} for non-default communities
    /// </summary>
    private static string GetCommunityPrefix(ICommunityContext community)
        => string.IsNullOrEmpty(community.CommunitySlug)
            || community.IsCustomDomain
            || community.IsDefaultCommunity
            ? ""
            : $"/c/{community.CommunitySlug}";

    /// <summary>
    /// Gets the URL prefix for an explicit community slug (used in cross-community listings).
    /// Compares against the default community slug to determine if a prefix is needed.
    /// </summary>
    private static string GetCommunityPrefix(string? communitySlug, ICommunityContext context)
        => string.IsNullOrEmpty(communitySlug)
            || context.IsCustomDomain
            || string.Equals(communitySlug, context.DefaultCommunitySlug, StringComparison.OrdinalIgnoreCase)
            ? ""
            : $"/c/{communitySlug}";

    /// <summary>
    /// Public accessor for the community prefix, used by inline JavaScript.
    /// </summary>
    public static string CommunityPrefix(ICommunityContext community)
        => GetCommunityPrefix(community);

    // ===== Community-aware URL methods =====

    public static string Community(string communitySlug, ICommunityContext context)
        => string.Equals(communitySlug, context.DefaultCommunitySlug, StringComparison.OrdinalIgnoreCase)
            ? "/"
            : $"/c/{communitySlug}";

    public static string Hub(
        ICommunityContext community,
        string hubSlug) 
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}";
    
    public static string HubWithOffset(
        ICommunityContext community,
        string hubSlug,
        int offset) 
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}?offset={offset}";

    public static string Space(
        ICommunityContext community,
        string hubSlug,
        string spaceSlug) 
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}/{spaceSlug}";

    public static string SpaceWithOffset(
        ICommunityContext community,
        string hubSlug,
        string spaceSlug,
        int offset) 
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}/{spaceSlug}?offset={offset}";

    public static string Discussion(
        ICommunityContext community,
        string hubSlug,
        string spaceSlug,
        string slugWithId)
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}/{spaceSlug}/{slugWithId}";

    public static string DiscussionWithOffset(
        ICommunityContext community,
        string hubSlug,
        string spaceSlug,
        string slugWithId,
        int offset)
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}/{spaceSlug}/{slugWithId}?offset={offset}";


    // ===== Explicit-slug overloads (for cross-community listings) =====
    // Used when rendering items from mixed communities (e.g., frontpage in multi-community mode).
    // The default community gets no /c/ prefix — its content lives at /h/... directly.

    public static string Hub(string communitySlug, ICommunityContext context, string hubSlug)
        => $"{GetCommunityPrefix(communitySlug, context)}/h/{hubSlug}";

    public static string Space(string communitySlug, ICommunityContext context, string hubSlug, string spaceSlug)
        => $"{GetCommunityPrefix(communitySlug, context)}/h/{hubSlug}/{spaceSlug}";

    public static string Discussion(string communitySlug, ICommunityContext context, string hubSlug, string spaceSlug, string slugWithId)
        => $"{GetCommunityPrefix(communitySlug, context)}/h/{hubSlug}/{spaceSlug}/{slugWithId}";

    // ===== Manage URL methods =====

    public static string ManageCommunity(ICommunityContext community)
        => $"{GetCommunityPrefix(community)}/manage";

    public static string ManageHub(ICommunityContext community, string hubSlug)
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}/manage";

    public static string ManageSpace(ICommunityContext community, string hubSlug, string spaceSlug)
        => $"{GetCommunityPrefix(community)}/h/{hubSlug}/s/{spaceSlug}/manage";

    public static string HubAvatar(string publicId, int revision = 0)
        => AvatarHelper.GetAvatarUrl(publicId, AvatarEntityType.Hub, revision);

    public static string SpaceAvatar(string publicId, int revision = 0)
        => AvatarHelper.GetAvatarUrl(publicId, AvatarEntityType.Space, revision);

    public static string CommunityAvatar(string publicId, int revision = 0)
        => AvatarHelper.GetAvatarUrl(publicId, AvatarEntityType.Community, revision);

    public static string UserAvatar(string publicId, int revision = 0)
        => AvatarHelper.GetAvatarUrl(publicId, AvatarEntityType.User, revision);

    // ===== Asset URL methods =====

    /// <summary>
    /// Gets the URL for a CSS file.
    /// - isVendor: true → /css/vendor/{filename}.css
    /// - isVendor: false → /css/dist/{filename}.css
    /// </summary>
    public static string Css(string filename, bool isVendor = false)
    {
        var extension = filename.EndsWith(".css") ? "" : ".css";
        var folder = isVendor ? "vendor" : "dist";

        return $"/css/{folder}/{filename}{extension}";
    }

    /// <summary>
    /// Gets the URL for a JavaScript file.
    /// - isVendor: true → /js/vendor/{filename}.js
    /// - isVendor: false → /js/dist/{filename}.js
    /// </summary>
    public static string Js(string filename, bool isVendor = false)
    {
        var extension = filename.EndsWith(".js") ? "" : ".js";
        var folder = isVendor ? "vendor" : "dist";

        return $"/js/{folder}/{filename}{extension}";
    }
}
