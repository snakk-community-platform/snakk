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

            // Steam OpenID 2.0 returns nameIdentifier as full URL — extract numeric SteamID64
            if (Provider.Equals("Steam", StringComparison.OrdinalIgnoreCase) &&
                nameIdentifier?.StartsWith("https://steamcommunity.com/openid/id/", StringComparison.OrdinalIgnoreCase) == true)
            {
                nameIdentifier = nameIdentifier.Split('/').Last();
            }

            if (string.IsNullOrEmpty(nameIdentifier))
            {
                logger.LogWarning("Missing nameIdentifier claim from {Provider}", Provider);
                return RedirectToPage("/Login", new { error = "oauth_claims_missing" });
            }

            // ── Connect-mode branch ──────────────────────────────────────
            if (HttpContext.Session.GetString("OAuth_ConnectMode") == "true")
            {
                HttpContext.Session.Remove("OAuth_ConnectMode");
                var connectUserId = HttpContext.Session.GetString("OAuth_ConnectUserId") ?? "";
                HttpContext.Session.Remove("OAuth_ConnectUserId");

                var connectReturnUrl = HttpContext.Session.GetString("OAuth_ReturnUrl") ?? "/settings/connected-accounts";
                HttpContext.Session.Remove("OAuth_ReturnUrl");
                if (!Url.IsLocalUrl(connectReturnUrl)) connectReturnUrl = "/settings/connected-accounts";

                try
                {
                    var connectReq = new ConnectOAuthProviderRequest
                    {
                        Provider = Provider.ToLowerInvariant(),
                        ProviderUserId = nameIdentifier,
                        UserPublicId = connectUserId
                    };
                    var connectResp = await authClient.ConnectOAuthProviderAsync(connectReq);
                    if (connectResp.Success)
                        return Redirect($"{connectReturnUrl}?connected={Uri.EscapeDataString(Provider.ToLowerInvariant())}");
                    return Redirect($"/settings/connected-accounts?error={Uri.EscapeDataString(connectResp.ErrorCode ?? "CONNECT_FAILED")}");
                }
                catch (RpcException ex)
                {
                    logger.LogWarning(ex, "ConnectOAuthProvider gRPC error for user {UserId}", connectUserId);
                    return Redirect($"/settings/connected-accounts?error=CONNECT_FAILED");
                }
            }

            // ── Sudo re-auth mode branch ─────────────────────────────────
            if (HttpContext.Session.GetString("OAuth_SudoMode") == "true")
            {
                HttpContext.Session.Remove("OAuth_SudoMode");
                var sudoUserId = HttpContext.Session.GetString("OAuth_SudoUserId") ?? "";
                HttpContext.Session.Remove("OAuth_SudoUserId");

                try
                {
                    var nonceReq = new GenerateOAuthSudoNonceRequest
                    {
                        UserPublicId = sudoUserId,
                        Provider = Provider.ToLowerInvariant(),
                        ProviderUserId = nameIdentifier
                    };
                    var nonceResp = await authClient.GenerateOAuthSudoNonceAsync(nonceReq);
                    var nonce = nonceResp.Nonce;

                    // Return a minimal page that posts the nonce to the parent window and closes
                    var html = $$"""
                        <!DOCTYPE html><html><body><script>
                        window.opener && window.opener.postMessage({type:'snakk:sudo:oauth-complete',nonce:'{{nonce}}'}, window.location.origin);
                        window.close();
                        </script></body></html>
                        """;
                    return Content(html, "text/html");
                }
                catch (RpcException ex)
                {
                    logger.LogWarning(ex, "GenerateOAuthSudoNonce gRPC error for user {UserId}", sudoUserId);
                    var html = """
                        <!DOCTYPE html><html><body><script>
                        window.opener && window.opener.postMessage({type:'snakk:sudo:oauth-failed'}, window.location.origin);
                        window.close();
                        </script></body></html>
                        """;
                    return Content(html, "text/html");
                }
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

            OAuthCallbackResponse response;
            try
            {
                response = await authClient.OAuthCallbackAsync(oauthRequest);
            }
            catch (RpcException ex) when (string.IsNullOrEmpty(email) && ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
            {
                // New user — provider didn't supply an email address
                logger.LogInformation("OAuth {Provider} did not return email — new user, redirecting to email entry", Provider);
                HttpContext.Session.SetString("OAuthEmail_Provider", Provider);
                HttpContext.Session.SetString("OAuthEmail_ProviderUserId", nameIdentifier);
                HttpContext.Session.SetString("OAuthEmail_DisplayName", name ?? "");
                if (!string.IsNullOrEmpty(inviteCode))
                    HttpContext.Session.SetString("OAuth_InviteCode", inviteCode);
                return RedirectToPage("/SetupEmail");
            }

            if (response.TwoFactorRequired)
            {
                var twoFactorReturnUrl = HttpContext.Session.GetString("OAuth_ReturnUrl") ?? "/";
                HttpContext.Session.Remove("OAuth_ReturnUrl");
                if (!Url.IsLocalUrl(twoFactorReturnUrl))
                    twoFactorReturnUrl = "/";
                // Use DB email when the provider (e.g. Steam) doesn't supply one
                var twoFactorEmail = !string.IsNullOrEmpty(email) ? email : response.User.Email;
                return Redirect($"/auth/twofactorverify?email={Uri.EscapeDataString(twoFactorEmail)}&returnUrl={Uri.EscapeDataString(twoFactorReturnUrl)}");
            }

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
