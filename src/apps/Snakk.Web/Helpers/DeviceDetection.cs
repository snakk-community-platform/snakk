namespace Snakk.Web.Helpers;

using System.Text.RegularExpressions;

public static partial class DeviceDetection
{
    // Mirrors Cloudflare's own UA classification used for CF-Device-Type.
    // Source: https://developers.cloudflare.com/cache/how-to/cache-rules/settings/#cache-key
    [GeneratedRegex(
        @"(?:phone|windows\s+phone|ipod|blackberry|(?:android|bb\d+|meego|silk|googlebot) .+? mobile|palm|windows\s+ce|opera mini|avantgo|mobilesafari|docomo|kaios)",
        RegexOptions.IgnoreCase)]
    private static partial Regex MobileUaRegex();

    // Called by the normalising middleware in Program.cs before OutputCache.
    public static bool IsMobileUserAgent(string userAgent)
        => MobileUaRegex().IsMatch(userAgent);

    // Reads the pre-computed X-Device-Type header set by the normalising middleware.
    // Safe to call in views, layout, and partials without re-running detection per render.
    public static bool IsMobile(HttpContext context)
        => context.Request.Headers["X-Device-Type"] == "mobile";
}
