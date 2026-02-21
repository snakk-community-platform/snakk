namespace Snakk.Web.Helpers;

using Snakk.Web.Services;
using Snakk.Shared.Helpers;

public static class SnakkUrlHelper
{
    /// <summary>
    /// Gets the URL prefix for a community.
    /// - Empty for default community (no prefix needed)
    /// - Empty for custom domains (domain itself identifies the community)
    /// - /c/{slug} for non-default communities accessed via main platform domain
    /// </summary>
    private static string GetCommunityPrefix(
        string? communitySlug,
        bool isDefaultCommunity,
        bool isCustomDomain = false) 
        => (string.IsNullOrEmpty(communitySlug) || isDefaultCommunity || isCustomDomain)
            ? "" // No prefix needed for default community or custom domains
            : $"/c/{communitySlug}";

    /// <summary>
    /// Gets the URL prefix for the current community context.
    /// </summary>
    private static string GetCommunityPrefix(ICommunityContext community)
        => GetCommunityPrefix(
            community.CommunitySlug,
            community.IsDefaultCommunity,
            community.IsCustomDomain);

    // ===== Community-aware URL methods =====

    public static string Community(string communitySlug) 
        => $"/c/{communitySlug}";

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


    // ===== Manage URL methods (always include /c/{slug} for gateway routing) =====

    public static string ManageCommunity(string communitySlug)
        => $"/c/{communitySlug}/manage";

    public static string ManageHub(string communitySlug, string hubSlug)
        => $"/c/{communitySlug}/h/{hubSlug}/manage";

    public static string ManageSpace(string communitySlug, string hubSlug, string spaceSlug)
        => $"/c/{communitySlug}/h/{hubSlug}/s/{spaceSlug}/manage";

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
