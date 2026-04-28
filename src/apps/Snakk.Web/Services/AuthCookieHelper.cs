namespace Snakk.Web.Services;

/// <summary>
/// Shared helper for managing authentication cookies.
/// Uses a dual-cookie pattern for SameSite security:
///   - .Snakk.Auth (Strict) — primary JWT, used for all state-changing operations
///   - .Snakk.Auth.Session (Lax) — same JWT, used for personalization on cross-site navigations
///   - .Snakk.Auth.Refresh (Strict) — refresh token
///
/// BFF mutation endpoints (POST/PUT/DELETE) MUST verify the Strict cookie is present.
/// The Lax session cookie alone is not sufficient for mutations.
/// </summary>
public static class AuthCookieHelper
{
    public const string AccessCookieName = ".Snakk.Auth";
    public const string SessionCookieName = ".Snakk.Auth.Session";
    public const string RefreshCookieName = ".Snakk.Auth.Refresh";

    public static void SetAuthCookies(HttpContext ctx, string accessToken, string refreshToken, bool rememberMe = false)
    {
        var expiry = rememberMe
            ? DateTimeOffset.UtcNow.AddDays(30)
            : DateTimeOffset.UtcNow.AddHours(8);

        // Primary auth cookie — SameSite=Strict (not sent on cross-site navigations)
        ctx.Response.Cookies.Append(AccessCookieName, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = expiry
        });

        // Session cookie — SameSite=Lax (sent on cross-site top-level navigations)
        // Used for personalization only (show username, avatar). NOT sufficient for mutations.
        ctx.Response.Cookies.Append(SessionCookieName, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expiry
        });

        // Refresh token — SameSite=Strict
        ctx.Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = expiry
        });
    }

    public static void DeleteAuthCookies(HttpContext ctx)
    {
        var deleteOptions = new CookieOptions { Path = "/" };
        ctx.Response.Cookies.Delete(AccessCookieName, deleteOptions);
        ctx.Response.Cookies.Delete(SessionCookieName, deleteOptions);
        ctx.Response.Cookies.Delete(RefreshCookieName, deleteOptions);
        ctx.Response.Cookies.Delete(TimezoneCookieName, deleteOptions);
    }

    /// <summary>
    /// Returns true if the Strict auth cookie is present on this request.
    /// BFF mutation endpoints should check this — the Lax session cookie alone is not enough.
    /// </summary>
    public static bool HasStrictAuthCookie(HttpContext ctx) =>
        ctx.Request.Cookies.ContainsKey(AccessCookieName);

    // Timezone cookie (non-httponly, long-lived)
    public const string TimezoneCookieName = ".Snakk.Pref.Timezone";

    public static void SetTimezoneCookie(HttpContext ctx, string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            ctx.Response.Cookies.Delete(TimezoneCookieName, new CookieOptions { Path = "/" });
            return;
        }

        ctx.Response.Cookies.Append(TimezoneCookieName, timezone, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(365)
        });
    }

    public static string? GetTimezone(HttpContext ctx)
    {
        var value = ctx.Request.Cookies[TimezoneCookieName];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    // Theme cookie (non-httponly, long-lived, values: "light" | "dark" | "auto")
    public const string ThemeCookieName = ".Snakk.Pref.Theme";

    public static void SetThemeCookie(HttpContext ctx, string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            ctx.Response.Cookies.Delete(ThemeCookieName, new CookieOptions { Path = "/" });
            return;
        }

        ctx.Response.Cookies.Append(ThemeCookieName, theme, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(365)
        });
    }
}
