using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using Grpc.Core;
using Snakk.Protos.Auth;

namespace Snakk.Auth.Pages;

public class RegisterModel(
    AuthService.AuthServiceClient authClient,
    IConfiguration configuration,
    ILogger<RegisterModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public bool HasGoogle => !string.IsNullOrEmpty(configuration["Authentication:Google:ClientId"]);
    public bool HasGitHub => !string.IsNullOrEmpty(configuration["Authentication:GitHub:ClientId"]);
    public bool HasDiscord => !string.IsNullOrEmpty(configuration["Authentication:Discord:ClientId"]);
    public bool HasAnyOAuth => HasGoogle || HasGitHub || HasDiscord;
    public string? TurnstileSiteKey => configuration["Turnstile:SiteKey"];

    public class InputModel
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = "";
    }

    public IActionResult OnGet()
    {
        if (Request.Cookies.ContainsKey(".Snakk.Auth"))
            return Redirect("/");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var registerRequest = new RegisterRequest
            {
                Email = Input.Email,
                Password = Input.Password,
                DisplayName = Input.DisplayName,
                BaseUrl = baseUrl
            };

            var turnstileToken = Request.Form["cf-turnstile-response"].FirstOrDefault();
            if (!string.IsNullOrEmpty(turnstileToken))
                registerRequest.TurnstileToken = turnstileToken;

            var response = await authClient.RegisterAsync(registerRequest);

            if (string.IsNullOrEmpty(response.AccessToken))
            {
                ErrorMessage = "Registration completed but login failed. Please try logging in.";
                return RedirectToPage("/Login", new { returnUrl = ReturnUrl });
            }

            // Set auth cookies using dual-cookie pattern
            var expiry = DateTimeOffset.UtcNow.AddHours(8);
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

            var returnUrl = ReturnUrl ?? "/";
            if (!Url.IsLocalUrl(returnUrl))
                returnUrl = "/";

            return Redirect(returnUrl);
        }
        catch (RpcException ex)
        {
            logger.LogWarning("Registration gRPC error: {Status}", ex.Status.Detail);
            ErrorMessage = ex.Status.Detail ?? "Registration failed. Please try again.";
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Registration error");
            ErrorMessage = "An error occurred during registration. Please try again.";
            return Page();
        }
    }
}
