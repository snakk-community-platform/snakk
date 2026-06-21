using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Snakk.Web.Helpers;
using Snakk.Web.Services;

namespace Snakk.Web.Pages;

public abstract class BasePageModel : PageModel
{
    protected readonly IConfiguration Configuration;
    protected readonly ICommunityContext CommunityContext;

    protected BasePageModel(IConfiguration configuration, ICommunityContext communityContext)
    {
        Configuration = configuration;
        CommunityContext = communityContext;
    }

    // ===== Feature CSS preloading =====

    private const string CssFeaturesKey = "snakk.css.features";

    [Microsoft.AspNetCore.Mvc.FromServices]
    public IFileVersionProvider? FileVersionProvider { get; set; }

    /// <summary>
    /// Declares a CSS feature needed by this page. Writes an HTTP Link preload header
    /// (so the browser fetches before parsing HTML) and stores the versioned path for
    /// the layout to emit as a stylesheet link.
    /// </summary>
    protected void Preload(string feature)
    {
        var basePath = SnakkUrlHelper.Feature(feature);
        var versioned = FileVersionProvider?.AddFileVersionToPath(HttpContext.Request.PathBase, basePath) ?? basePath;
        Response.Headers.Append("Link", $"<{versioned}>; rel=preload; as=style");
        CssFeatureList.Add(versioned);
    }

    /// <summary>
    /// Versioned feature CSS paths declared via Preload(), read by _Layout.cshtml.
    /// </summary>
    public IReadOnlyList<string> CssFeatures => CssFeatureList;

    private List<string> CssFeatureList
    {
        get
        {
            if (!HttpContext.Items.TryGetValue(CssFeaturesKey, out var val))
                HttpContext.Items[CssFeaturesKey] = val = new List<string>();
            return (List<string>)val!;
        }
    }

    // Common helper methods that can be used across all pages

    public string GetRelativeTime(DateTime? dateTime)
    {
        if (!dateTime.HasValue) return "";
        var diff = DateTime.UtcNow - dateTime.Value;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalDays < 365) return dateTime.Value.ToString("MMM d");
        return dateTime.Value.ToString("MMM d, yyyy");
    }

    public string GetRelativeTime(DateTimeOffset? dateTime)
    {
        if (!dateTime.HasValue) return "";
        var diff = DateTimeOffset.UtcNow - dateTime.Value;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalDays < 365) return dateTime.Value.ToString("MMM d");
        return dateTime.Value.ToString("MMM d, yyyy");
    }

    // Site settings cache — injected via [FromServices] (no change to derived class constructors)
    [Microsoft.AspNetCore.Mvc.FromServices]
    public SiteSettingsCacheService? SiteSettingsCache { get; set; }

    /// <summary>
    /// The effective timezone for this request.
    /// Cascade: user cookie → community timezone → site timezone (DB, cached) → site timezone (config) → UTC
    /// </summary>
    public string EffectiveTimezone =>
        AuthCookieHelper.GetTimezone(HttpContext)
        ?? CommunityContext.CommunityTimezone
        ?? SiteSettingsCache?.CachedTimezone
        ?? Configuration["Snakk:SiteTimezone"]
        ?? "UTC";

    public string FormatRelativeTime(DateTime dateTime)
    {
        var now = DateTime.UtcNow;
        var diff = now - dateTime;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(EffectiveTimezone);
            var local = TimeZoneInfo.ConvertTimeFromUtc(dateTime, tz);
            return diff.TotalDays < 365 ? local.ToString("MMM d") : local.ToString("MMM d, yyyy");
        }
        catch
        {
            return diff.TotalDays < 365 ? dateTime.ToString("MMM d") : dateTime.ToString("MMM d, yyyy");
        }
    }

    public string FormatRelativeTime(DateTimeOffset dateTime)
        => FormatRelativeTime(dateTime.UtcDateTime);

    public string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
    }

    public string GetAvatarColor(string? role)
    {
        // Simplified - all users get the same neutral avatar style
        return "author-avatar-simple";
    }

    public string EscapeForJs(string content) =>
        content
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "");

    public string EscapeForHtmlAttribute(string content) =>
        System.Net.WebUtility.HtmlEncode(content);

    // Common properties
    public string ApiBaseUrl => Configuration["ApiBaseUrl"] ?? "https://localhost:17101";
    public ICommunityContext Community => CommunityContext;
    public bool ShowCommunityInBreadcrumb =>
        CommunityContext.IsMultiCommunityEnabled
        && !CommunityContext.IsCustomDomain;
}
