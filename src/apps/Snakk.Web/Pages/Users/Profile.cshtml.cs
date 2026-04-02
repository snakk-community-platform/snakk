using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Shared.Helpers;
using Snakk.Web.Services;
using Snakk.Protos.User;

namespace Snakk.Web.Pages.Users;

[OutputCache(PolicyName = "AnonymousProfile")]
public class ProfileModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public UserProfileInfo? Profile { get; set; }

    public string FormatDate(DateTimeOffset? dateTime)
    {
        if (!dateTime.HasValue) return "Unknown";
        return FormatRelativeTime(dateTime.Value);
    }

    public async Task<IActionResult> OnGetAsync(string publicId)
    {
        string decodedPublicId;
        try { decodedPublicId = UlidBase62.Decode(publicId); }
        catch { return NotFound(); }

        var profileResult = await _apiClient.GetUserProfileResultAsync(decodedPublicId);

        if (!profileResult.IsSuccess)
            return profileResult.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        Profile = profileResult.Value;

        return Page();
    }
}
