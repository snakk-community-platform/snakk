using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Grpc.Core;
using Snakk.Protos.Auth;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Auth;

public class LoginModel(AuthService.AuthServiceClient authClient) : PageModel
{
    public string? ErrorMessage { get; set; }

    public void OnGet(string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            ErrorMessage = error switch
            {
                "oauth_failed" => "OAuth authentication failed. Please try again.",
                "invalid_oauth_response" => "Invalid response from OAuth provider.",
                _ => Uri.UnescapeDataString(error)
            };
        }
    }

    public async Task<IActionResult> OnPostAsync(string email, string password, string? returnUrl)
    {
        try
        {
            var response = await authClient.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = password,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
            });

            if (string.IsNullOrEmpty(response.AccessToken) || string.IsNullOrEmpty(response.RefreshToken))
            {
                ErrorMessage = "Authentication succeeded but tokens were empty.";
                return Page();
            }

            AuthCookieHelper.SetAuthCookies(HttpContext, response.AccessToken, response.RefreshToken);

            var redirectUrl = returnUrl ?? "/";
            if (!Url.IsLocalUrl(redirectUrl))
            {
                redirectUrl = "/";
            }

            return Redirect(redirectUrl);
        }
        catch (RpcException ex)
        {
            ErrorMessage = ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated
                ? "Invalid email or password."
                : "Login failed. Please try again.";
            return Page();
        }
    }
}
