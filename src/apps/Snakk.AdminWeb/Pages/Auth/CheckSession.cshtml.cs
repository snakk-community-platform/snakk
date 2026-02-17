using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Sdk;
using System.IdentityModel.Tokens.Jwt;

namespace Snakk.AdminWeb.Pages.Auth;

public class CheckSessionModel : PageModel
{
    private readonly SnakkApiClient _apiClient;

    public CheckSessionModel(SnakkApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IResult> OnGetAsync()
    {
        var accessToken = Request.Cookies["admin_token"];

        // If no token or expired, try to refresh
        if (string.IsNullOrEmpty(accessToken) || IsTokenExpiringSoon(accessToken, minutes: 5))
        {
            var refreshToken = Request.Cookies["admin_refresh_token"];
            if (string.IsNullOrEmpty(refreshToken))
            {
                return Results.Json(new { authenticated = false, reason = "no_refresh_token" });
            }

            try
            {
                // Call the SDK refresh endpoint
                var result = await _apiClient.RefreshTokenAsync(new RefreshTokenRequest
                {
                    RefreshToken = refreshToken
                });

                if (result == null || string.IsNullOrEmpty(result.AccessToken))
                {
                    return Results.Json(new { authenticated = false, reason = "no_token_returned" });
                }

                // Update cookies
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

                return Results.Json(new { authenticated = true, refreshed = true });
            }
            catch (Exception ex)
            {
                return Results.Json(new { authenticated = false, reason = "error", error = ex.Message });
            }
        }

        // Token is still valid
        return Results.Json(new { authenticated = true, refreshed = false });
    }

    private bool IsTokenExpiringSoon(string token, int minutes)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
                return true;

            var jwtToken = handler.ReadJwtToken(token);
            return (jwtToken.ValidTo - DateTime.UtcNow).TotalMinutes < minutes;
        }
        catch
        {
            return true;
        }
    }
}
