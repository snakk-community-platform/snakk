using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Auth;
using System.ComponentModel.DataAnnotations;

namespace Snakk.Auth.Pages;

[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class ForgotPasswordModel(
    AuthService.AuthServiceClient authClient,
    ILogger<ForgotPasswordModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool EmailSent { get; private set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email address")]
        public string Email { get; set; } = "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

            await authClient.RequestPasswordResetAsync(new RequestPasswordResetRequest
            {
                Email = Input.Email,
                BaseUrl = baseUrl,
                IpAddress = ipAddress,
                UserAgent = userAgent
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning("RequestPasswordReset gRPC error: {Status}", ex.Status.Detail);
            // Fall through — always show success to prevent enumeration
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RequestPasswordReset unexpected error");
            // Fall through — always show success to prevent enumeration
        }

        EmailSent = true;
        return Page();
    }
}
