using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Services;

namespace Snakk.Web.Pages;

public class ProfileModel(SnakkApiClient apiClient) : PageModel
{
    public new UserInfo? User { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var currentUser = await apiClient.GetCurrentUserAsync();

        if (currentUser is null)
        {
            return Page();
        }

        User = new UserInfo
        {
            PublicId = currentUser.PublicId,
            DisplayName = currentUser.DisplayName,
            Email = currentUser.Email,
            EmailVerified = currentUser.EmailVerified,
            OAuthProvider = currentUser.OauthProvider
        };

        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await apiClient.LogoutAsync();

        // Clear cookies
        foreach (var cookie in Request.Cookies.Keys)
        {
            Response.Cookies.Delete(cookie);
        }

        return RedirectToPage("/Index");
    }

    public class UserInfo
    {
        public string PublicId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Email { get; set; }
        public bool EmailVerified { get; set; }
        public string? OAuthProvider { get; set; }
    }
}
