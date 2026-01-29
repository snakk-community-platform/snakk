using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Models;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Users;

public class ProfileModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public UserProfileDto? Profile { get; set; }

    public string FormatDate(DateTime? dateTime)
    {
        if (!dateTime.HasValue) return "Unknown";
        return dateTime.Value.ToString("MMMM d, yyyy");
    }

    public new string GetRelativeTime(DateTime? dateTime)
    {
        if (!dateTime.HasValue) return "Never";
        var diff = DateTime.UtcNow - dateTime.Value;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} minutes ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hours ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
        if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)} weeks ago";
        return dateTime.Value.ToString("MMM d, yyyy");
    }

    public async Task<IActionResult> OnGetAsync(string publicId)
    {
        Profile = await _apiClient.GetUserProfileAsync(publicId);

        if (Profile == null)
            return NotFound();

        return Page();
    }
}
