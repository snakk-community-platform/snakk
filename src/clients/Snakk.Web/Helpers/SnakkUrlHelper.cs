namespace Snakk.Web.Helpers;

using Snakk.Web.Services;

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

    public static string HubAvatar(string publicId)
        => $"/storage/avatars/generated/hubs/{publicId}.svg";

    public static string SpaceAvatar(string publicId)
        => $"/storage/avatars/generated/spaces/{publicId}.svg";

    public static string CommunityAvatar(string publicId)
        => $"/storage/avatars/generated/communities/{publicId}.svg";

    public static string UserAvatar(string publicId)
        => $"/storage/avatars/generated/users/{publicId}.svg";
}
