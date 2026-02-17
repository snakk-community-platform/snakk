using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Sdk;
using System.Security.Claims;

namespace Snakk.AdminWeb.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly SnakkApiClient _apiClient;
    private readonly ILogger<LoginModel> _logger;

    public string? ErrorMessage { get; set; }
    public string? ReturnUrl { get; set; }

    public LoginModel(SnakkApiClient apiClient, ILogger<LoginModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string username, string password, string? returnUrl = null)
    {
        try
        {
            var request = new LoginRequest
            {
                Email = username,
                Password = password
            };

            var result = await _apiClient.LoginAsync(request);

            if (result == null)
            {
                ErrorMessage = "Invalid username or password.";
                ReturnUrl = returnUrl;
                return Page();
            }

            // Check if user has admin role
            if (!result.User.Roles.Contains("GlobalAdmin") && !result.User.Roles.Contains("CommunityAdmin"))
            {
                ErrorMessage = "Access denied. You must be an admin to access this panel.";
                ReturnUrl = returnUrl;
                return Page();
            }

            // Create claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, result.User.DisplayName),
                new Claim(ClaimTypes.Email, result.User.Email),
            };

            foreach (var role in result.User.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Store tokens in cookies
            Response.Cookies.Append("admin_token", result.AccessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(8)
            });

            Response.Cookies.Append("admin_refresh_token", result.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            // Redirect
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for user {Username}", username);
            ErrorMessage = "An error occurred during login. Please try again.";
            ReturnUrl = returnUrl;
            return Page();
        }
    }
}
