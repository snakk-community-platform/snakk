using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using Grpc.Core;
using Snakk.Protos.Auth;

namespace Snakk.Auth.Pages;

public class LoginModel(
    AuthService.AuthServiceClient authClient,
    IConfiguration configuration,
    ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public bool HasGoogle => !string.IsNullOrEmpty(configuration["Authentication:Google:ClientId"]);
    public bool HasGitHub => !string.IsNullOrEmpty(configuration["Authentication:GitHub:ClientId"]);
    public bool HasDiscord => !string.IsNullOrEmpty(configuration["Authentication:Discord:ClientId"]);
    public bool HasAnyOAuth => HasGoogle || HasGitHub || HasDiscord;
    public string? TurnstileSiteKey => configuration["Turnstile:SiteKey"];

    public class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public IActionResult OnGet(string? error)
    {
        var accessToken = Request.Cookies[".Snakk.Auth"];
        if (!string.IsNullOrEmpty(accessToken) && IsTokenValid(accessToken))
            return Redirect("/");

        // Token is missing, expired or malformed — delete stale auth cookies
        // so the user starts from a clean state before logging in.
        if (Request.Cookies.ContainsKey(".Snakk.Auth") ||
            Request.Cookies.ContainsKey(".Snakk.Auth.Session") ||
            Request.Cookies.ContainsKey(".Snakk.Auth.Refresh"))
        {
            var deleteOptions = new CookieOptions { Path = "/" };
            Response.Cookies.Delete(".Snakk.Auth",         deleteOptions);
            Response.Cookies.Delete(".Snakk.Auth.Session", deleteOptions);
            Response.Cookies.Delete(".Snakk.Auth.Refresh", deleteOptions);
        }

        Input.ReturnUrl = ReturnUrl;

        if (!string.IsNullOrEmpty(error))
        {
            ErrorMessage = error switch
            {
                "oauth_failed" => "OAuth authentication failed. Please try again.",
                "invalid_oauth_response" => "Invalid response from OAuth provider.",
                "oauth_claims_missing" => "Missing required information from OAuth provider.",
                "oauth_token_missing" => "Authentication succeeded but no token was returned.",
                "oauth_server_error" => "A server error occurred during authentication.",
                "oauth_error" => "An error occurred during authentication.",
                _ => Uri.UnescapeDataString(error)
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            var loginRequest = new LoginRequest
            {
                Email = Input.Email,
                Password = Input.Password,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
            };

            var turnstileToken = Request.Form["cf-turnstile-response"].FirstOrDefault();
            if (!string.IsNullOrEmpty(turnstileToken))
                loginRequest.TurnstileToken = turnstileToken;

            var response = await authClient.LoginAsync(loginRequest);

            // Check if 2FA is required
            if (response.TwoFactorRequired)
            {
                var encodedEmail = Uri.EscapeDataString(Input.Email);
                var encodedReturn = Uri.EscapeDataString(Input.ReturnUrl ?? ReturnUrl ?? "/");
                return Redirect($"/auth/twofactorverify?email={encodedEmail}&returnUrl={encodedReturn}");
            }

            if (string.IsNullOrEmpty(response.AccessToken))
            {
                ErrorMessage = "Login failed. Please try again.";
                return Page();
            }

            // Set auth cookies using dual-cookie pattern:
            //   .Snakk.Auth (Strict) — used for state-changing operations
            //   .Snakk.Auth.Session (Lax) — used for personalization on cross-site navigations
            //   .Snakk.Auth.Refresh (Strict) — refresh token
            var expiry = Input.RememberMe
                ? DateTimeOffset.UtcNow.AddDays(30)
                : DateTimeOffset.UtcNow.AddHours(8);

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
            if (Input.RememberMe)
                Response.Cookies.Append(".Snakk.Pref.RememberMe", "1", new CookieOptions
                {
                    HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax,
                    Path = "/", Expires = DateTimeOffset.UtcNow.AddDays(30)
                });
            else
                Response.Cookies.Delete(".Snakk.Pref.RememberMe", new CookieOptions { Path = "/" });

            var returnUrl = Input.ReturnUrl ?? ReturnUrl ?? "/";
            if (!Url.IsLocalUrl(returnUrl))
                returnUrl = "/";

            // Check if user needs to accept new consents
            try
            {
                var consentHeaders = new Grpc.Core.Metadata { { "authorization", $"Bearer {response.AccessToken}" } };
                var consentClient = HttpContext.RequestServices.GetRequiredService<Snakk.Protos.Consent.ConsentService.ConsentServiceClient>();
                var pending = await consentClient.GetPendingConsentsAsync(
                    new Snakk.Protos.Consent.GetPendingConsentsRequest(), headers: consentHeaders);

                if (pending.Consents.Count > 0)
                {
                    var encodedReturn = Uri.EscapeDataString(returnUrl);
                    return Redirect($"/auth/consent?returnUrl={encodedReturn}");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to check consent status after login");
                // Don't block login if consent check fails
            }

            return Redirect(returnUrl);
        }
        catch (RpcException ex)
        {
            logger.LogWarning("Login gRPC error: {Status}", ex.Status.Detail);
            ErrorMessage = ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated
                ? "Invalid email or password."
                : "Login failed. Please try again.";
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login error");
            ErrorMessage = "An error occurred during login. Please try again.";
            return Page();
        }
    }

    private static bool IsTokenValid(string token)
    {
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token)) return false;
            var jwt = handler.ReadJwtToken(token);
            return jwt.ValidTo > DateTime.UtcNow.AddSeconds(30);
        }
        catch { return false; }
    }
}
