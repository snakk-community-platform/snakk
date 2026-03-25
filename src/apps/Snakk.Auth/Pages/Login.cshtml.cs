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

    public void OnGet(string? error)
    {
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
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            var response = await authClient.LoginAsync(new LoginRequest
            {
                Email = Input.Email,
                Password = Input.Password,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
            });

            if (string.IsNullOrEmpty(response.AccessToken))
            {
                ErrorMessage = "Login failed. Please try again.";
                return Page();
            }

            // Set auth cookies (access + refresh)
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = Input.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(8),
                Path = "/"
            };

            Response.Cookies.Append(".Snakk.Auth", response.AccessToken, cookieOptions);
            if (!string.IsNullOrEmpty(response.RefreshToken))
            {
                Response.Cookies.Append(".Snakk.Auth.Refresh", response.RefreshToken, cookieOptions);
            }

            var returnUrl = Input.ReturnUrl ?? ReturnUrl ?? "/";
            if (!Url.IsLocalUrl(returnUrl))
                returnUrl = "/";

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
}
