using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Auth;

namespace Snakk.Auth.Pages;

public class LogoutModel(
    AuthService.AuthServiceClient authClient,
    ILogger<LogoutModel> logger) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        await RevokeAndClearAsync();
        return Redirect("/");
    }

    public async Task<IActionResult> OnPostAsync() => await OnGetAsync();

    private async Task RevokeAndClearAsync()
    {
        var accessToken = Request.Cookies[".Snakk.Auth"];
        if (!string.IsNullOrEmpty(accessToken))
        {
            try
            {
                var headers = new Metadata { { "authorization", $"Bearer {accessToken}" } };
                await authClient.LogoutAsync(new LogoutRequest(), headers);
            }
            catch (Exception ex)
            {
                // Don't block logout if API is unavailable — cookies are cleared regardless.
                logger.LogWarning(ex, "Failed to revoke refresh token during logout");
            }
        }

        var deleteOptions = new CookieOptions { Path = "/" };
        Response.Cookies.Delete(".Snakk.Auth",            deleteOptions);
        Response.Cookies.Delete(".Snakk.Auth.Session",    deleteOptions);
        Response.Cookies.Delete(".Snakk.Auth.Refresh",    deleteOptions);
        Response.Cookies.Delete(".Snakk.Pref.RememberMe", deleteOptions);
        Response.Cookies.Delete(".Snakk.Pref.Timezone",   deleteOptions);
    }
}
