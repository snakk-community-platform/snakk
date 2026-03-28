namespace Snakk.Api.Middleware;

using System.IdentityModel.Tokens.Jwt;
using Snakk.Application.Services;

public class TokenRefreshMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITokenService tokenService)
    {
        // Check if access token exists and is expiring soon
        var accessToken = context.Request.Cookies["access_token"];

        if (!string.IsNullOrEmpty(accessToken))
        {
            var expiration = tokenService.GetTokenExpiration(accessToken);

            if (expiration.HasValue)
            {
                var timeUntilExpiry = expiration.Value - DateTime.UtcNow;

                // If token expires in less than 5 minutes, refresh proactively
                if (timeUntilExpiry.TotalMinutes < 5 && timeUntilExpiry.TotalMinutes > 0)
                {
                    var refreshToken = context.Request.Cookies["refresh_token"];

                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        try
                        {
                            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                            var newAccessToken = await tokenService.RefreshAccessTokenAsync(refreshToken, ipAddress, context.Request.Headers.UserAgent.ToString());

                            if (newAccessToken is not null)
                            {
                                // Update access token cookie
                                context.Response.Cookies.Append("access_token", newAccessToken, new CookieOptions
                                {
                                    HttpOnly = true,
                                    Secure = true,
                                    SameSite = SameSiteMode.Strict,
                                    Expires = DateTimeOffset.UtcNow.AddMinutes(30)
                                });
                            }
                        }
                        catch
                        {
                            // Refresh failed - let the request proceed and handle auth failure normally
                        }
                    }
                }
            }
        }

        await next(context);
    }
}
