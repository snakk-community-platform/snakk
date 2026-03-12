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
    /// Whether the current community is the default community.
    /// When true, URLs should omit the /c/{community} prefix.
    /// </summary>
    bool IsDefaultCommunity { get; }

    /// <summary>
    /// Whether the request came via a custom domain.
    /// When true, URLs should not include the /c/{community} prefix
    /// because the domain itself identifies the community.
    /// </summary>
    bool IsCustomDomain { get; }

    /// <summary>
    /// Whether multi-community mode is enabled (Features:MultiCommunityEnabled).
    /// </summary>
    bool IsMultiCommunityEnabled { get; }

    /// <summary>
    /// The slug of the default community (from configuration).
    /// Used by URL helpers to determine whether a given slug needs a /c/{slug} prefix.
    /// The default community never gets a prefix — its content lives at /h/... directly.
    /// </summary>
    string DefaultCommunitySlug { get; }

    /// <summary>
    /// The IANA timezone of the current community. Null if not set or not resolved from custom domain.
    /// </summary>
    string? CommunityTimezone { get; }

    /// <summary>
    /// Sets the current community context.
    /// </summary>
    void SetCommunity(
        string slug,
        bool isDefault,
        bool isCustomDomain = false,
        string? name = null,
        bool isMultiCommunity = false,
        string? defaultCommunitySlug = null,
        string? timezone = null);
}

/// <summary>
/// Scoped service that holds the current community context for a request.
/// </summary>
public class CommunityContext : ICommunityContext
{
    public string? CommunitySlug { get; private set; }
    public string? CommunityName { get; private set; }
    public bool IsDefaultCommunity { get; private set; } = true;
    public bool IsCustomDomain { get; private set; }
    public bool IsMultiCommunityEnabled { get; private set; }
    public string DefaultCommunitySlug { get; private set; } = "main";
    public string? CommunityTimezone { get; private set; }

    public void SetCommunity(
        string slug,
        bool isDefault,
        bool isCustomDomain = false,
        string? name = null,
        bool isMultiCommunity = false,
        string? defaultCommunitySlug = null,
        string? timezone = null)
    {
        CommunitySlug = slug;
        CommunityName = name;
        IsDefaultCommunity = isDefault;
        IsCustomDomain = isCustomDomain;
        IsMultiCommunityEnabled = isMultiCommunity;
        CommunityTimezone = timezone;
        if (defaultCommunitySlug is not null)
            DefaultCommunitySlug = defaultCommunitySlug;
    }
}
