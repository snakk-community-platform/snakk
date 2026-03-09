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
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Display name must be between 2 and 50 characters.")]
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = "";
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var accessToken = Request.Cookies[".Snakk.Auth"];

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

            // Update the auth cookie if a new token was returned (with updated display name)
            if (!string.IsNullOrEmpty(response.Token))
            {
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    Path = "/"
                };
                Response.Cookies.Append(".Snakk.Auth", response.Token, cookieOptions);
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
