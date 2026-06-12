using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Auth;
using System.ComponentModel.DataAnnotations;

namespace Snakk.Auth.Pages;

[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class ResetPasswordModel(
    AuthService.AuthServiceClient authClient,
    ILogger<ResetPasswordModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? Token { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool TokenMissing { get; private set; }

    public class InputModel
    {
        [Required(ErrorMessage = "New password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = "";

        [Required(ErrorMessage = "Please confirm your new password")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = "";

        public string? Token { get; set; }
    }

    public IActionResult OnGet(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TokenMissing = true;
            return Page();
        }

        Token = token;
        Input.Token = token;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Token = Input.Token;
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Input.Token))
        {
            TokenMissing = true;
            return Page();
        }

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

            await authClient.ResetPasswordAsync(new ResetPasswordRequest
            {
                Token = Input.Token,
                NewPassword = Input.NewPassword,
                IpAddress = ipAddress,
                UserAgent = userAgent
            });

            return Redirect("/auth/login?passwordReset=1");
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            Token = Input.Token;
            ErrorMessage = ex.Status.Detail;
            return Page();
        }
        catch (RpcException ex)
        {
            logger.LogWarning("ResetPassword gRPC error: {Status}", ex.Status.Detail);
            Token = Input.Token;
            ErrorMessage = "An error occurred. Please try again or request a new reset link.";
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ResetPassword unexpected error");
            Token = Input.Token;
            ErrorMessage = "An error occurred. Please try again.";
            return Page();
        }
    }
}
