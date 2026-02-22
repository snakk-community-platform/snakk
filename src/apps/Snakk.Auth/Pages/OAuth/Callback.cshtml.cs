using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Snakk.Auth.Pages.OAuth;

public class CallbackModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CallbackModel> _logger;

    public CallbackModel(IHttpClientFactory httpClientFactory, ILogger<CallbackModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

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
                _logger.LogWarning("OAuth authentication failed for provider: {Provider}", Provider);
                return RedirectToPage("/Login", new { error = "oauth_failed" });
            }

            // Extract user info from claims
            var claims = authenticateResult.Principal?.Claims;
            var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var nameIdentifier = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(nameIdentifier))
            {
                _logger.LogWarning("Missing required OAuth claims");
                return RedirectToPage("/Login", new { error = "oauth_claims_missing" });
            }

            // Call API to login or create account with OAuth
            var httpClient = _httpClientFactory.CreateClient("SnakkApi");

            var oauthLoginRequest = new
            {
                provider = Provider.ToLower(),
                providerUserId = nameIdentifier,
                email = email,
                displayName = name ?? email.Split('@')[0]
            };

            var content = new StringContent(
                JsonSerializer.Serialize(oauthLoginRequest),
                Encoding.UTF8,
                "application/json"
            );

            var response = await httpClient.PostAsync("/api/auth/oauth/callback", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("OAuth callback failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return RedirectToPage("/Login", new { error = "oauth_server_error" });
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var loginResponse = JsonSerializer.Deserialize<OAuthResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (loginResponse?.AccessToken == null)
            {
                return RedirectToPage("/Login", new { error = "oauth_token_missing" });
            }

            // Set auth cookies (access + refresh)
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                Path = "/"
            };

            Response.Cookies.Append(".Snakk.Auth", loginResponse.AccessToken, cookieOptions);
            if (!string.IsNullOrEmpty(loginResponse.RefreshToken))
            {
                Response.Cookies.Append(".Snakk.Auth.Refresh", loginResponse.RefreshToken, cookieOptions);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth callback error");
            return RedirectToPage("/Login", new { error = "oauth_error" });
        }
    }

    private class OAuthResponse
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
