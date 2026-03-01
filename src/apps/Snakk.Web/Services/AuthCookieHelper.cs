namespace Snakk.Web.Services;

/// <summary>
/// Shared helper for managing authentication cookies.
/// Used by BFF endpoints and GrpcAuthInterceptor for consistent cookie handling.
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
        ctx.Response.Cookies.Delete(PreferEndlessScrollCookieName, new CookieOptions { Path = "/" });
    }

    // User preference cookies (non-httponly, long-lived)
    public const string PreferEndlessScrollCookieName = ".Snakk.Pref.EndlessScroll";

    public static void SetPreferenceCookies(HttpContext ctx, bool preferEndlessScroll)
    {
        var options = new CookieOptions
        {
            HttpOnly = false, // Not sensitive — readable by JS if needed
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(365)
        };
        ctx.Response.Cookies.Append(PreferEndlessScrollCookieName, preferEndlessScroll ? "1" : "0", options);
    }

    public static bool GetPreferEndlessScroll(HttpContext ctx)
    {
        var value = ctx.Request.Cookies[PreferEndlessScrollCookieName];
        return value != "0"; // Default to true
    }
}
