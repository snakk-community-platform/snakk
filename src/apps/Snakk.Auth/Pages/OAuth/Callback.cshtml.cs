using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Grpc.Core;
using Snakk.Protos.Auth;

namespace Snakk.Auth.Pages.OAuth;

public class CallbackModel(AuthService.AuthServiceClient authClient, ILogger<CallbackModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Provider { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            // The OAuth handler signs the principal into the default (cookie) scheme
            // during its CallbackPath handling, then redirects here. CSRF is already
            // covered by the handler's correlation cookie — no need to duplicate it.
            var authenticateResult = await HttpContext.AuthenticateAsync();

            if (!authenticateResult.Succeeded)
            {
                logger.LogWarning("OAuth authentication failed for {Provider}: {Reason}",
                    Provider, authenticateResult.Failure?.Message ?? "NoResult");
                return RedirectToPage("/Login", new { error = "oauth_failed" });
            }

            // Extract user info from claims
            var claims = authenticateResult.Principal?.Claims;
            var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var nameIdentifier = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(nameIdentifier))
            {
                logger.LogWarning("Missing required OAuth claims");
                return RedirectToPage("/Login", new { error = "oauth_claims_missing" });
            }

            // Call API via gRPC to login or create account with OAuth
            var inviteCode = HttpContext.Session.GetString("OAuth_InviteCode");
            HttpContext.Session.Remove("OAuth_InviteCode");

            var oauthRequest = new OAuthCallbackRequest
            {
                Provider = Provider.ToLower(),
                ProviderUserId = nameIdentifier,
                Email = email,
                DisplayName = name ?? "",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
            };
            if (!string.IsNullOrEmpty(inviteCode))
                oauthRequest.InviteCode = inviteCode;

            var response = await authClient.OAuthCallbackAsync(oauthRequest);

            if (string.IsNullOrEmpty(response.AccessToken))
            {
                return RedirectToPage("/Login", new { error = "oauth_token_missing" });
            }

            // Set auth cookies using dual-cookie pattern.
            // Honor "remember me" preference stored during OAuth initiation.
            var rememberMe = HttpContext.Session.GetString("OAuth_RememberMe") == "true";
            HttpContext.Session.Remove("OAuth_RememberMe");
            var expiry = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8);
            var strictOptions = new CookieOptions
            {
                HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/", Expires = expiry
            };
            var laxOptions = new CookieOptions
            {
                HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = "/", Expires = expiry
            };

            Response.Cookies.Append(".Snakk.Auth", response.AccessToken, strictOptions);
            Response.Cookies.Append(".Snakk.Auth.Session", response.AccessToken, laxOptions);
            if (!string.IsNullOrEmpty(response.RefreshToken))
            {
                Response.Cookies.Append(".Snakk.Auth.Refresh", response.RefreshToken, strictOptions);
            }

            // Persist remember-me preference so token refresh in Snakk.Web honors it
            if (rememberMe)
                Response.Cookies.Append(".Snakk.Pref.RememberMe", "1", new CookieOptions
                {
                    HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax,
                    Path = "/", Expires = DateTimeOffset.UtcNow.AddDays(30)
                });
            else
                Response.Cookies.Delete(".Snakk.Pref.RememberMe", new CookieOptions { Path = "/" });

            // New users go to profile setup page to choose their display name
            if (response.IsNewUser)
            {
                return Redirect("/auth/setup-profile");
            }

            // Get return URL from session
            var returnUrl = HttpContext.Session.GetString("OAuth_ReturnUrl") ?? "/";
            HttpContext.Session.Remove("OAuth_ReturnUrl");

            // Validate return URL
            if (!Url.IsLocalUrl(returnUrl))
            {
                returnUrl = "/";
            }

            return Redirect(returnUrl);
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "OAuth gRPC callback error: {Status}", ex.Status.Detail);
            var errorCode = ex.Status.Detail switch
            {
                "REGISTRATION_CLOSED" => "registration_closed",
                "INVITE_CODE_REQUIRED" => "invite_required",
                "INVITE_CODE_INVALID" => "invite_invalid",
                _ => null
            };
            if (errorCode is not null)
                return RedirectToPage("/Register", new { error = errorCode });
            return RedirectToPage("/Login", new { error = "oauth_server_error" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OAuth callback error");
            return RedirectToPage("/Login", new { error = "oauth_error" });
        }
    }
}
