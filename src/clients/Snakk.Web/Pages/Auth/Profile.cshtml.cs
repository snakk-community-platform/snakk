using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Auth;

public class ProfileModel(SnakkApiClient apiClient, IConfiguration configuration) : PageModel
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public string? UserId { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public string? OAuthProvider { get; set; }
    public bool PreferEndlessScroll { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public string ApiBaseUrl => configuration["ApiBaseUrl"] ?? "https://localhost:7291";

    public async Task<IActionResult> OnGetAsync()
    {
        var authStatus = await _apiClient.GetAuthStatusAsync();
        if (authStatus == null || !authStatus.IsAuthenticated)
        {
            return RedirectToPage("/Auth/Login", new { returnUrl = "/auth/profile" });
        }

        UserId = authStatus.PublicId;
        DisplayName = authStatus.DisplayName;

        // Get full user details
        var userDetails = await _apiClient.GetCurrentUserAsync();
        if (userDetails != null)
        {
            Email = userDetails.Email;
            EmailVerified = userDetails.EmailVerified;
            OAuthProvider = userDetails.OAuthProvider;
            PreferEndlessScroll = userDetails.PreferEndlessScroll;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateDisplayNameAsync(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            ErrorMessage = "Display name cannot be empty";
            return await OnGetAsync();
        }

        var success = await _apiClient.UpdateProfileAsync(displayName);
        if (success)
        {
            SuccessMessage = "Display name updated successfully";
        }
        else
        {
            ErrorMessage = "Failed to update display name";
        }

        return await OnGetAsync();
    }

    public async Task<IActionResult> OnPostUpdateScrollPreferenceAsync(bool preferEndlessScroll)
    {
        var success = await _apiClient.UpdatePreferencesAsync(preferEndlessScroll);
        if (success)
        {
            SuccessMessage = "Scroll preference updated successfully";
        }
        else
        {
            ErrorMessage = "Failed to update scroll preference";
        }

        return await OnGetAsync();
    }
}
