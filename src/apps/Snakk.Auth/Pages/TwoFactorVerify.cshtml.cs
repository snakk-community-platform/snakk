using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using Grpc.Core;
using Snakk.Protos.TwoFactor;

namespace Snakk.Auth.Pages;

[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth-2fa")]
public class TwoFactorVerifyModel(
    TwoFactorService.TwoFactorServiceClient twoFactorClient,
    ILogger<TwoFactorVerifyModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Provider { get; set; }

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [StringLength(20, MinimumLength = 6)]
        [Display(Name = "Authentication Code")]
        public string Code { get; set; } = "";

        [Display(Name = "Trust this device")]
        public bool TrustDevice { get; set; }
    }

    public IActionResult OnGet()
    {
        // Authoritative email comes from the server session set after first-factor auth,
        // never from the query string (S3). No session → the password step never happened.
        var pendingEmail = Snakk.Auth.Services.TwoFactorLoginSession.GetEmail(HttpContext.Session);
        if (string.IsNullOrEmpty(pendingEmail))
            return RedirectToPage("/Login");

        Email = pendingEmail;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Re-read the pending email from the session, ignoring any client-supplied value.
        var pendingEmail = Snakk.Auth.Services.TwoFactorLoginSession.GetEmail(HttpContext.Session);
        if (string.IsNullOrEmpty(pendingEmail))
            return RedirectToPage("/Login");

        Email = pendingEmail;

        if (!ModelState.IsValid)
            return Page();

        try
        {
            var response = await twoFactorClient.VerifyTwoFactorLoginAsync(
                new VerifyTwoFactorLoginRequest
                {
                    Email = pendingEmail,
                    Code = Input.Code.Trim().Replace(" ", ""),
                    TrustDevice = Input.TrustDevice,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                    UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
                });

            if (string.IsNullOrEmpty(response.AccessToken))
            {
                ErrorMessage = response.Error is { Length: > 0 } ? response.Error : "Verification failed. Please try again.";
                return Page();
            }

            // First factor + second factor both satisfied — clear the pending marker.
            Snakk.Auth.Services.TwoFactorLoginSession.Clear(HttpContext.Session);

            var rememberMe = Request.Cookies[".Snakk.Pref.RememberMe"] == "1";
            var expiry = rememberMe
                ? DateTimeOffset.UtcNow.AddDays(30)
                : DateTimeOffset.UtcNow.AddHours(8);
            var strictOptions = new CookieOptions
            {
                HttpOnly = true, Secure = Snakk.Shared.Helpers.AuthCookieSecurity.RequireSecure, SameSite = SameSiteMode.Strict, Path = "/", Expires = expiry
            };
            var laxOptions = new CookieOptions
            {
                HttpOnly = true, Secure = Snakk.Shared.Helpers.AuthCookieSecurity.RequireSecure, SameSite = SameSiteMode.Lax, Path = "/", Expires = expiry
            };

            Response.Cookies.Append(".Snakk.Auth", response.AccessToken, strictOptions);
            Response.Cookies.Append(".Snakk.Auth.Session", response.AccessToken, laxOptions);
            if (!string.IsNullOrEmpty(response.RefreshToken))
                Response.Cookies.Append(".Snakk.Auth.Refresh", response.RefreshToken, strictOptions);

            var returnUrl = ReturnUrl ?? "/";
            if (!Url.IsLocalUrl(returnUrl))
                returnUrl = "/";

            var destination = !string.IsNullOrEmpty(Provider)
                ? $"/post-login?provider={Uri.EscapeDataString(Provider)}&to={Uri.EscapeDataString(returnUrl)}"
                : returnUrl;
            return Redirect(destination);
        }
        catch (RpcException ex)
        {
            logger.LogWarning("2FA verify gRPC error: {Status}", ex.Status.Detail);
            ErrorMessage = "Invalid code. Please try again.";
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "2FA verify error");
            ErrorMessage = "An error occurred. Please try again.";
            return Page();
        }
    }
}
