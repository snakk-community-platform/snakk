using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using Grpc.Core;
using Snakk.Protos.Auth;

namespace Snakk.Auth.Pages;

public class RegisterModel(
    AuthService.AuthServiceClient authClient,
    Snakk.Protos.Consent.ConsentService.ConsentServiceClient consentClient,
    IConfiguration configuration,
    ILogger<RegisterModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string RegistrationMode { get; set; } = "Open";

    public bool HasGoogle => !string.IsNullOrEmpty(configuration["Authentication:Google:ClientId"]);
    public bool HasGitHub => !string.IsNullOrEmpty(configuration["Authentication:GitHub:ClientId"]);
    public bool HasDiscord => !string.IsNullOrEmpty(configuration["Authentication:Discord:ClientId"]);
    public bool HasAnyOAuth => HasGoogle || HasGitHub || HasDiscord;
    public string? TurnstileSiteKey => configuration["Turnstile:SiteKey"];

    public List<Snakk.Protos.Consent.ConsentTypeInfo> RequiredConsents { get; set; } = [];

    public class InputModel
    {
        [Required]
        [StringLength(20, MinimumLength = 3)]
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        [RegularExpression(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$",
            ErrorMessage = "Password must contain uppercase, lowercase, a number, and a special character.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = "";

        public string? InviteCode { get; set; }

        [Required(ErrorMessage = "Please choose whether to allow adult content.")]
        public bool? AllowAdultContent { get; set; }
    }

    public async Task<IActionResult> OnGet(string? error = null)
    {
        if (Request.Cookies.ContainsKey(".Snakk.Auth"))
            return Redirect("/");

        try
        {
            var publicSettings = await authClient.GetPublicSettingsAsync(new GetPublicSettingsRequest());
            RegistrationMode = string.IsNullOrEmpty(publicSettings.RegistrationMode)
                ? "Open"
                : publicSettings.RegistrationMode;
        }
        catch { /* Don't block page load */ }

        ErrorMessage = error switch
        {
            "registration_closed" => "Registration is currently closed.",
            "invite_required" => "An invite code is required to create a new account. Please enter it below before signing up.",
            "invite_invalid" => "Invalid invite code. Please check and try again.",
            _ => null
        };

        if (RegistrationMode == "Closed")
            return Page();

        try
        {
            var consents = await consentClient.GetRequiredConsentsAsync(
                new Snakk.Protos.Consent.GetRequiredConsentsRequest());
            RequiredConsents = consents.Consents.ToList();
        }
        catch { /* Don't block registration page load */ }

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
                BaseUrl = baseUrl,
                AllowAdultContent = Input.AllowAdultContent!.Value
            };

            var turnstileToken = Request.Form["cf-turnstile-response"].FirstOrDefault();
            if (!string.IsNullOrEmpty(turnstileToken))
                registerRequest.TurnstileToken = turnstileToken;

            if (!string.IsNullOrEmpty(Input.InviteCode))
                registerRequest.InviteCode = Input.InviteCode;

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
            Response.Cookies.Delete(".Snakk.Pref.RememberMe", new CookieOptions { Path = "/" });

            // Record consents
            var versionIds = Request.Form["consentVersionId"]
                .Select(v => int.TryParse(v, out var id) ? id : 0)
                .Where(id => id > 0)
                .ToList();

            if (versionIds.Count > 0)
            {
                try
                {
                    var consentHeaders = new Grpc.Core.Metadata { { "authorization", $"Bearer {response.AccessToken}" } };
                    await consentClient.AcceptConsentsAsync(
                        new Snakk.Protos.Consent.AcceptConsentsRequest
                        {
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                            VersionIds = { versionIds }
                        }, headers: consentHeaders);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to record consents during registration");
                }
            }

            var returnUrl = ReturnUrl ?? "/";
            if (!Url.IsLocalUrl(returnUrl))
                returnUrl = "/";

            return Redirect(returnUrl);
        }
        catch (RpcException ex)
        {
            logger.LogWarning("Registration gRPC error: {Status}", ex.Status.Detail);
            ErrorMessage = ex.Status.Detail switch
            {
                "REGISTRATION_CLOSED" => "Registration is currently closed.",
                "INVITE_CODE_REQUIRED" => "An invite code is required to register.",
                "INVITE_CODE_INVALID" => "Invalid invite code. Please check and try again.",
                _ => ex.Status.Detail ?? "Registration failed. Please try again."
            };
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Registration error");
            ErrorMessage = "An error occurred during registration. Please try again.";
            return Page();
        }
    }

    // Called when user clicks an OAuth button — stores invite code in session then redirects to OAuth challenge
    public IActionResult OnPostOAuth(string provider, string? inviteCode)
    {
        if (!string.IsNullOrWhiteSpace(inviteCode))
            HttpContext.Session.SetString("OAuth_InviteCode", inviteCode.Trim());

        return RedirectToPage("/OAuth/Challenge", new { provider, returnUrl = ReturnUrl });
    }
}
