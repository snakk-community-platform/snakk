using Microsoft.AspNetCore.Mvc.RazorPages;
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

    public string FormatRelativeTime(DateTime dateTime)
    {
        var now = DateTime.UtcNow;
        var diff = now - dateTime;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalDays < 365) return dateTime.ToString("MMM d");
        return dateTime.ToString("MMM d, yyyy");
    }

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

    public string EscapeForJs(string content)
    {
        return content
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "");
    }

    public string EscapeForHtmlAttribute(string content)
    {
        return System.Net.WebUtility.HtmlEncode(content);
    }

    public string GetUserAvatar(string apiBaseUrl, string userPublicId)
    {
        return $"{apiBaseUrl}/avatars/{userPublicId}";
    }

    public string GetSpaceAvatar(string apiBaseUrl, string spacePublicId)
    {
        return $"{apiBaseUrl}/spaces/{spacePublicId}/avatar";
    }

    public string GetCommunityAvatar(string apiBaseUrl, string communityPublicId)
    {
        return $"{apiBaseUrl}/communities/{communityPublicId}/avatar";
    }

    // Common properties
    public string ApiBaseUrl => Configuration["ApiBaseUrl"] ?? "https://localhost:7291";
    public ICommunityContext Community => CommunityContext;
    public bool ShowCommunityInBreadcrumb =>
        Configuration.GetValue<bool>("Features:MultiCommunityEnabled") &&
        !CommunityContext.IsDefaultCommunity &&
        !CommunityContext.IsCustomDomain;
}
