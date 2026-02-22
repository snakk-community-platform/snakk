namespace Snakk.Web.Services;

/// <summary>
/// Shared helper for managing authentication cookies.
/// Used by BFF endpoints and CookieForwardingHandler for consistent cookie handling.
/// </summary>
public static class AuthCookieHelper
{
    public const string AccessCookieName = ".Snakk.Auth";
    public const string RefreshCookieName = ".Snakk.Auth.Refresh";

    public static CookieOptions CreateOptions(bool isHttps, bool rememberMe = false)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = rememberMe
                ? DateTimeOffset.UtcNow.AddDays(30)
                : DateTimeOffset.UtcNow.AddHours(8)
        };
    }

    public static void SetAuthCookies(HttpContext ctx, string accessToken, string refreshToken, bool rememberMe = false)
    {
        var options = CreateOptions(ctx.Request.IsHttps, rememberMe);
        ctx.Response.Cookies.Append(AccessCookieName, accessToken, options);
        ctx.Response.Cookies.Append(RefreshCookieName, refreshToken, options);
    }

    public static void DeleteAuthCookies(HttpContext ctx)
    {
        ctx.Response.Cookies.Delete(AccessCookieName, new CookieOptions { Path = "/" });
        ctx.Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = "/" });
    }
}
