using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using Grpc.Core;
using Snakk.Protos.Auth;

namespace Snakk.Auth.Pages;

[IgnoreAntiforgeryToken]
public class SetupEmailModel(
    AuthService.AuthServiceClient authClient,
    ILogger<SetupEmailModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ProviderName { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool IsPostAuthFlow { get; private set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";
    }

    public IActionResult OnGet()
    {
        ProviderName = HttpContext.Session.GetString("OAuthEmail_Provider");
        if (!string.IsNullOrEmpty(ProviderName))
            return Page();

        // Middleware redirect: authenticated user with no email in JWT
        if (User.Identity?.IsAuthenticated == true)
        {
            IsPostAuthFlow = true;
            return Page();
        }

        return RedirectToPage("/Login", new { error = "oauth_failed" });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ProviderName = HttpContext.Session.GetString("OAuthEmail_Provider");

        if (string.IsNullOrEmpty(ProviderName) && User.Identity?.IsAuthenticated != true)
            return RedirectToPage("/Login", new { error = "oauth_failed" });

        if (string.IsNullOrEmpty(ProviderName))
        {
            // Post-auth flow: set email on existing account
            IsPostAuthFlow = true;

            if (!ModelState.IsValid)
                return Page();

            var token = Request.Cookies[".Snakk.Auth"] ?? Request.Cookies[".Snakk.Auth.Session"];
            if (string.IsNullOrEmpty(token))
                return RedirectToPage("/Login");

            try
            {
                var headers = new Grpc.Core.Metadata { { "authorization", $"Bearer {token}" } };
                var response = await authClient.SetEmailAsync(
                    new SetEmailRequest { Email = Input.Email },
                    headers);

                if (!string.IsNullOrEmpty(response.Token))
                {
                    var expiry = DateTimeOffset.UtcNow.AddHours(8);
                    Response.Cookies.Append(".Snakk.Auth", response.Token, new CookieOptions
                    {
                        HttpOnly = true, Secure = Snakk.Shared.Helpers.AuthCookieSecurity.RequireSecure, SameSite = SameSiteMode.Strict, Path = "/", Expires = expiry
                    });
                    Response.Cookies.Append(".Snakk.Auth.Session", response.Token, new CookieOptions
                    {
                        HttpOnly = true, Secure = Snakk.Shared.Helpers.AuthCookieSecurity.RequireSecure, SameSite = SameSiteMode.Lax, Path = "/", Expires = expiry
                    });
                }

                var postAuthReturnUrl = HttpContext.Session.GetString("OAuth_ReturnUrl") ?? "/";
                HttpContext.Session.Remove("OAuth_ReturnUrl");
                if (!Url.IsLocalUrl(postAuthReturnUrl)) postAuthReturnUrl = "/";
                return Redirect(postAuthReturnUrl);
            }
            catch (RpcException ex)
            {
                logger.LogWarning("SetEmail gRPC error: {Status}", ex.Status.Detail);
                ErrorMessage = ex.Status.Detail switch
                {
                    "EMAIL_TAKEN" => "This email is already in use by another account.",
                    "EMAIL_ALREADY_SET" => "Your account already has an email address.",
                    _ => "Failed to save email. Please try again."
                };
                return Page();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SetEmail error");
                ErrorMessage = "An error occurred. Please try again.";
                return Page();
            }
        }

        if (!ModelState.IsValid)
            return Page();

        var provider       = ProviderName;
        var providerUserId = HttpContext.Session.GetString("OAuthEmail_ProviderUserId") ?? "";
        var displayName    = HttpContext.Session.GetString("OAuthEmail_DisplayName") ?? "";
        var inviteCode     = HttpContext.Session.GetString("OAuth_InviteCode");
        var returnUrl      = HttpContext.Session.GetString("OAuth_ReturnUrl") ?? "/";
        var rememberMe     = HttpContext.Session.GetString("OAuth_RememberMe") == "true";

        try
        {
            var oauthRequest = new OAuthCallbackRequest
            {
                Provider      = provider.ToLower(),
                ProviderUserId = providerUserId,
                Email         = Input.Email,
                DisplayName   = displayName,
                IpAddress     = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                UserAgent     = HttpContext.Request.Headers.UserAgent.ToString()
            };
            if (!string.IsNullOrEmpty(inviteCode))
                oauthRequest.InviteCode = inviteCode;

            var response = await authClient.OAuthCallbackAsync(oauthRequest);

            HttpContext.Session.Remove("OAuthEmail_Provider");
            HttpContext.Session.Remove("OAuthEmail_ProviderUserId");
            HttpContext.Session.Remove("OAuthEmail_DisplayName");
            HttpContext.Session.Remove("OAuth_InviteCode");
            HttpContext.Session.Remove("OAuth_ReturnUrl");
            HttpContext.Session.Remove("OAuth_RememberMe");

            if (response.TwoFactorRequired)
            {
                if (!Url.IsLocalUrl(returnUrl)) returnUrl = "/";
                return Redirect($"/auth/twofactorverify?email={Uri.EscapeDataString(Input.Email)}&returnUrl={Uri.EscapeDataString(returnUrl)}");
            }

            if (string.IsNullOrEmpty(response.AccessToken))
            {
                ErrorMessage = "Authentication failed. Please try again.";
                return Page();
            }

            var expiry = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8);
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

            if (rememberMe)
                Response.Cookies.Append(".Snakk.Pref.RememberMe", "1", new CookieOptions
                {
                    HttpOnly = true, Secure = Snakk.Shared.Helpers.AuthCookieSecurity.RequireSecure, SameSite = SameSiteMode.Lax,
                    Path = "/", Expires = DateTimeOffset.UtcNow.AddDays(30)
                });
            else
                Response.Cookies.Delete(".Snakk.Pref.RememberMe", new CookieOptions { Path = "/" });

            if (response.IsNewUser)
                return Redirect("/auth/setup-profile");

            if (!Url.IsLocalUrl(returnUrl)) returnUrl = "/";
            return Redirect(returnUrl);
        }
        catch (RpcException ex)
        {
            logger.LogWarning("SetupEmail gRPC error: {Status}", ex.Status.Detail);
            ErrorMessage = ex.StatusCode switch
            {
                Grpc.Core.StatusCode.AlreadyExists => "This email is already linked to a different account.",
                _ => "Authentication failed. Please try again."
            };
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SetupEmail error");
            ErrorMessage = "An error occurred. Please try again.";
            return Page();
        }
    }
}
