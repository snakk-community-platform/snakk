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
            // Authenticate with OAuth provider
            var authenticateResult = await HttpContext.AuthenticateAsync(Provider);

            if (!authenticateResult.Succeeded)
            {
                logger.LogWarning("OAuth authentication failed for provider: {Provider}", Provider);
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
            var response = await authClient.OAuthCallbackAsync(new OAuthCallbackRequest
            {
                Provider = Provider.ToLower(),
                ProviderUserId = nameIdentifier,
                Email = email,
                DisplayName = name ?? email.Split('@')[0],
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
            });

            if (string.IsNullOrEmpty(response.AccessToken))
            {
                return RedirectToPage("/Login", new { error = "oauth_token_missing" });
            }

            // Set auth cookies (access + refresh)
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Always secure — browsers treat localhost as secure context
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                Path = "/"
            };

            Response.Cookies.Append(".Snakk.Auth", response.AccessToken, cookieOptions);
            if (!string.IsNullOrEmpty(response.RefreshToken))
            {
                Response.Cookies.Append(".Snakk.Auth.Refresh", response.RefreshToken, cookieOptions);
            }

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
            return RedirectToPage("/Login", new { error = "oauth_server_error" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OAuth callback error");
            return RedirectToPage("/Login", new { error = "oauth_error" });
        }
    }
}
