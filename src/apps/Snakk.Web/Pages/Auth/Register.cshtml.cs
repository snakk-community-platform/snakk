using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Grpc.Core;
using Snakk.Protos.Auth;

namespace Snakk.Web.Pages.Auth;

public class RegisterModel(AuthService.AuthServiceClient authClient) : PageModel
{
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string email, string password, string displayName)
    {
        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            await authClient.RegisterAsync(new RegisterRequest
            {
                Email = email,
                Password = password,
                DisplayName = displayName,
                BaseUrl = baseUrl
            });

            SuccessMessage = "Account created successfully! Please check your email to verify your account before logging in.";
            return Page();
        }
        catch (RpcException ex)
        {
            ErrorMessage = ex.Status.Detail ?? "Registration failed. Please try again.";
            return Page();
        }
    }
}
