using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using Grpc.Core;
using Snakk.Protos.Auth;

namespace Snakk.Auth.Pages;

public class SetupProfileModel(
    AuthService.AuthServiceClient authClient,
    ILogger<SetupProfileModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Display name is required.")]
        [StringLength(20, MinimumLength = 3, ErrorMessage = "Display name must be between 3 and 20 characters.")]
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = "";

        [Required(ErrorMessage = "Please choose whether to allow adult content.")]
        public bool? AllowAdultContent { get; set; }
    }

    public IActionResult OnGet()
    {
        if (string.IsNullOrEmpty(Request.Cookies[".Snakk.Auth"] ?? Request.Cookies[".Snakk.Auth.Session"]))
            return RedirectToPage("/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var accessToken = Request.Cookies[".Snakk.Auth"]
            ?? Request.Cookies[".Snakk.Auth.Session"];

        if (string.IsNullOrEmpty(accessToken))
        {
            ErrorMessage = "Session expired. Please sign in again.";
            return RedirectToPage("/Login");
        }

        try
        {
            // Pass the JWT as gRPC metadata so the API knows which user to update
            var headers = new Metadata { { "authorization", $"Bearer {accessToken}" } };

            var response = await authClient.UpdateProfileAsync(
                new UpdateProfileRequest { DisplayName = Input.DisplayName.Trim() },
                headers);

            try
            {
                await authClient.UpdatePreferencesAsync(
                    new UpdatePreferencesRequest { AllowAdultContent = Input.AllowAdultContent!.Value },
                    headers);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to persist adult-content preference during OAuth profile setup");
            }

            // Update the auth cookie if a new token was returned (with updated display name)
            if (!string.IsNullOrEmpty(response.Token))
            {
                var expiry = DateTimeOffset.UtcNow.AddDays(30);
                var strictOptions = new CookieOptions
                {
                    HttpOnly = true, Secure = Snakk.Shared.Helpers.AuthCookieSecurity.RequireSecure, SameSite = SameSiteMode.Strict, Path = "/", Expires = expiry
                };
                var laxOptions = new CookieOptions
                {
                    HttpOnly = true, Secure = Snakk.Shared.Helpers.AuthCookieSecurity.RequireSecure, SameSite = SameSiteMode.Lax, Path = "/", Expires = expiry
                };
                Response.Cookies.Append(".Snakk.Auth", response.Token, strictOptions);
                Response.Cookies.Append(".Snakk.Auth.Session", response.Token, laxOptions);
            }

            return Redirect("/");
        }
        catch (RpcException ex)
        {
            logger.LogWarning("Profile update gRPC error: {Status}", ex.Status.Detail);
            ErrorMessage = ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated
                ? "Session expired. Please sign in again."
                : "Failed to update profile. Please try again.";
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Profile setup error");
            ErrorMessage = "An error occurred. Please try again.";
            return Page();
        }
    }
}
