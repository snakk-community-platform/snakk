using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Snakk.Auth.Pages;

public class LoginModel(
    IHttpClientFactory httpClientFactory,
    IConfiguration _configuration,
    ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

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

    public void OnGet()
    {
        Input.ReturnUrl = ReturnUrl;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var httpClient = httpClientFactory.CreateClient("SnakkApi");

            // Call API login endpoint
            var loginRequest = new
            {
                email = Input.Email,
                password = Input.Password
            };

            var content = new StringContent(
                JsonSerializer.Serialize(loginRequest),
                Encoding.UTF8,
                "application/json"
            );

            var response = await httpClient.PostAsync("/auth/login", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogWarning("Login failed: {StatusCode} - {Error}", response.StatusCode, errorContent);

                ErrorMessage = "Invalid email/username or password.";
                return Page();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (loginResponse?.AccessToken is null)
            {
                ErrorMessage = "Login failed. Please try again.";
                return Page();
            }

            // Set auth cookies (access + refresh)
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Always secure — browsers treat localhost as secure context
                SameSite = SameSiteMode.Lax,
                Expires = Input.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8),
                Path = "/"
            };

            Response.Cookies.Append(".Snakk.Auth", loginResponse.AccessToken, cookieOptions);
            if (!string.IsNullOrEmpty(loginResponse.RefreshToken))
            {
                Response.Cookies.Append(".Snakk.Auth.Refresh", loginResponse.RefreshToken, cookieOptions);
            }

            // Redirect to return URL or home
            var returnUrl = Input.ReturnUrl ?? ReturnUrl ?? "/";

            // Validate return URL to prevent open redirect
            if (!Url.IsLocalUrl(returnUrl))
            {
                returnUrl = "/";
            }

            return Redirect(returnUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login error");
            ErrorMessage = "An error occurred during login. Please try again.";
            return Page();
        }
    }

    private class LoginResponse
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
