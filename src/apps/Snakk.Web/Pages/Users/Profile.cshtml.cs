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
        return FormatRelativeTime(dateTime.Value);
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
