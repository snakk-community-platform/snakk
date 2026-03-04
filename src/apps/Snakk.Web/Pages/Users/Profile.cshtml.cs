using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Services;
using Snakk.Protos.User;

namespace Snakk.Web.Pages.Users;

public class ProfileModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public UserProfileInfo? Profile { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "overview";

    public string FormatDate(DateTimeOffset? dateTime)
    {
        if (!dateTime.HasValue) return "Unknown";
        return dateTime.Value.ToString("MMMM d, yyyy");
    }

    public new string GetRelativeTime(DateTimeOffset? dateTime)
    {
        if (!dateTime.HasValue) return "Never";
        var diff = DateTimeOffset.UtcNow - dateTime.Value;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} minutes ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hours ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
        if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)} weeks ago";
        return dateTime.Value.ToString("MMM d, yyyy");
    }

    public async Task<IActionResult> OnGetAsync(string publicId)
    {
        var profileResult = await _apiClient.GetUserProfileResultAsync(publicId);

        if (!profileResult.IsSuccess)
            return profileResult.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        Profile = profileResult.Value;

        // Validate tab parameter
        if (!new[] { "overview", "discussions", "posts" }.Contains(Tab))
            Tab = "overview";

        return Page();
    }
}
