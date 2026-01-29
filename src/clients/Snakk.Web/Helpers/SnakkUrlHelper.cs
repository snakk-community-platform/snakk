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
    private static string GetCommunityPrefix(string? communitySlug, bool isDefaultCommunity, bool isCustomDomain = false)
    {
        // No prefix needed for default community or custom domains
        if (string.IsNullOrEmpty(communitySlug) || isDefaultCommunity || isCustomDomain)
            return "";
        return $"/c/{communitySlug}";
    }

    /// <summary>
    /// Gets the URL prefix for the current community context.
    /// </summary>
    private static string GetCommunityPrefix(ICommunityContext community)
    {
        return GetCommunityPrefix(community.CommunitySlug, community.IsDefaultCommunity, community.IsCustomDomain);
    }

    // ===== Community-aware URL methods =====

    public static string Community(string communitySlug)
    {
        return $"/c/{communitySlug}";
    }

    public static string Hub(ICommunityContext community, string hubSlug)
    {
        return $"{GetCommunityPrefix(community)}/h/{hubSlug}";
    }

    public static string HubWithOffset(ICommunityContext community, string hubSlug, int offset)
    {
        return $"{GetCommunityPrefix(community)}/h/{hubSlug}?offset={offset}";
    }

    public static string Space(ICommunityContext community, string hubSlug, string spaceSlug)
    {
        return $"{GetCommunityPrefix(community)}/h/{hubSlug}/{spaceSlug}";
    }

    public static string SpaceWithOffset(ICommunityContext community, string hubSlug, string spaceSlug, int offset)
    {
        return $"{GetCommunityPrefix(community)}/h/{hubSlug}/{spaceSlug}?offset={offset}";
    }

    public static string Discussion(ICommunityContext community, string hubSlug, string spaceSlug, string slugWithId)
    {
        return $"{GetCommunityPrefix(community)}/h/{hubSlug}/{spaceSlug}/{slugWithId}";
    }

    public static string DiscussionWithOffset(ICommunityContext community, string hubSlug, string spaceSlug, string slugWithId, int offset)
    {
        return $"{GetCommunityPrefix(community)}/h/{hubSlug}/{spaceSlug}/{slugWithId}?offset={offset}";
    }

    // ===== Legacy methods (assume default community) =====
    // These are kept for backward compatibility and will be phased out

    public static string Hub(string hubSlug)
    {
        return $"/h/{hubSlug}";
    }

    public static string HubWithOffset(string hubSlug, int offset)
    {
        return $"/h/{hubSlug}?offset={offset}";
    }

    public static string Space(string hubSlug, string spaceSlug)
    {
        return $"/h/{hubSlug}/{spaceSlug}";
    }

    public static string SpaceWithOffset(string hubSlug, string spaceSlug, int offset)
    {
        return $"/h/{hubSlug}/{spaceSlug}?offset={offset}";
    }

    public static string Discussion(string hubSlug, string spaceSlug, string slugWithId)
    {
        return $"/h/{hubSlug}/{spaceSlug}/{slugWithId}";
    }

    public static string DiscussionWithOffset(string hubSlug, string spaceSlug, string slugWithId, int offset)
    {
        return $"/h/{hubSlug}/{spaceSlug}/{slugWithId}?offset={offset}";
    }

    // ===== Utility methods =====

    /// <summary>
    /// Formats a count number as a compact string (e.g., 1000 -> "1k", 1500 -> "1.5k")
    /// </summary>
    public static string FormatCount(int count)
    {
        return count switch
        {
            >= 1_000_000 => $"{count / 1_000_000.0:0.#}m",
            >= 1_000 => $"{count / 1_000.0:0.#}k",
            _ => count.ToString()
        };
    }

    // Avatar URL helpers - entities use .svg extension for CDN caching
    public static string HubAvatar(string apiBaseUrl, string publicId) => $"{apiBaseUrl}/avatars/hub/{publicId}.svg";
    public static string SpaceAvatar(string apiBaseUrl, string publicId) => $"{apiBaseUrl}/avatars/space/{publicId}.svg";
    public static string CommunityAvatar(string apiBaseUrl, string publicId) => $"{apiBaseUrl}/avatars/community/{publicId}.svg";
    public static string UserAvatar(string apiBaseUrl, string publicId) => $"{apiBaseUrl}/avatars/user/{publicId}.svg";
}
