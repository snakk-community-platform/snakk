using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Models;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Moderation;

public class BansModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    public IEnumerable<UserBanDto>? Bans { get; set; }
    public UserProfileDto? UserProfile { get; set; }
    public bool CanModerate { get; set; }

    [BindProperty]
    public BanUserRequest? BanRequest { get; set; }

    public bool IsActiveBan(UserBanDto ban)
    {
        if (ban.UnbannedAt.HasValue) return false;
        if (ban.ExpiresAt.HasValue && ban.ExpiresAt.Value < DateTime.UtcNow) return false;
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        CanModerate = await apiClient.CanModerateAsync();
        if (!CanModerate)
        {
            return RedirectToPage("/Index");
        }

        if (!string.IsNullOrEmpty(UserId))
        {
            UserProfile = await apiClient.GetUserProfileAsync(UserId);
            Bans = await apiClient.GetUserBansAsync(UserId);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostBanAsync()
    {
        if (BanRequest == null || string.IsNullOrEmpty(BanRequest.TargetUserId))
        {
            return RedirectToPage(new { UserId });
        }

        await apiClient.BanUserAsync(BanRequest);
        return RedirectToPage(new { UserId = BanRequest.TargetUserId });
    }

    public async Task<IActionResult> OnPostUnbanAsync(string banId, string userId)
    {
        await apiClient.UnbanUserAsync(banId);
        return RedirectToPage(new { UserId = userId });
    }
}
