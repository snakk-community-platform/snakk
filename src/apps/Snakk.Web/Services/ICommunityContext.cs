namespace Snakk.Web.Services;

/// <summary>
/// Provides access to the current community context resolved from the request URL.
/// </summary>
public interface ICommunityContext
{
    /// <summary>
    /// The slug of the current community. Null if not yet resolved.
    /// </summary>
    string? CommunitySlug { get; }

    /// <summary>
    /// The display name of the current community. Null if not yet resolved or not available.
    /// </summary>
    string? CommunityName { get; }

    /// <summary>
    /// Whether the request came via a custom domain.
    /// When true, URLs should not include the /c/{community} prefix
    /// because the domain itself identifies the community.
    /// </summary>
    bool IsCustomDomain { get; }

    /// <summary>
    /// Whether multi-community mode is enabled (Features:MultiCommunityEnabled).
    /// When false, there is no community concept exposed to users — the single community IS the site.
    /// When true, all community content lives under /c/{slug}/ and /h/ shortcuts do not exist.
    /// </summary>
    bool IsMultiCommunityEnabled { get; }

    /// <summary>
    /// The IANA timezone of the current community. Null if not set or not resolved from custom domain.
    /// </summary>
    string? CommunityTimezone { get; }

    /// <summary>
    /// Sets the current community context.
    /// </summary>
    void SetCommunity(
        string slug,
        bool isCustomDomain = false,
        string? name = null,
        bool isMultiCommunity = false,
        string? timezone = null);
}

public static class CommunityContextExtensions
{
    /// <summary>True when community badges/links should be shown in multi-community mode without a scoped domain.</summary>
    public static bool ShouldShowCommunity(this ICommunityContext ctx)
        => ctx.IsMultiCommunityEnabled && string.IsNullOrEmpty(ctx.CommunitySlug) && !ctx.IsCustomDomain;
}

/// <summary>
/// Scoped service that holds the current community context for a request.
/// </summary>
public class CommunityContext : ICommunityContext
{
    public string? CommunitySlug { get; private set; }
    public string? CommunityName { get; private set; }
    public bool IsCustomDomain { get; private set; }
    public bool IsMultiCommunityEnabled { get; private set; }
    public string? CommunityTimezone { get; private set; }

    public void SetCommunity(
        string slug,
        bool isCustomDomain = false,
        string? name = null,
        bool isMultiCommunity = false,
        string? timezone = null)
    {
        CommunitySlug = slug;
        CommunityName = name;
        IsCustomDomain = isCustomDomain;
        IsMultiCommunityEnabled = isMultiCommunity;
        CommunityTimezone = timezone;
    }
}
