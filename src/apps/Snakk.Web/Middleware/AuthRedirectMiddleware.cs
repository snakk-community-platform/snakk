namespace Snakk.Web.Middleware;

/// <summary>
/// Middleware that redirects unauthenticated users to SSO login when attempting protected actions.
/// GET requests are allowed for viewing content, but modifying requests (POST, PUT, DELETE, PATCH)
/// require authentication.
/// </summary>
public class AuthRedirectMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly HashSet<string> ModifyingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "DELETE", "PATCH"
    };

    public AuthRedirectMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;

        // Skip auth check for static files and auth/BFF endpoints
        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        // Only require auth for modifying requests (POST, PUT, DELETE, PATCH)
        if (ModifyingMethods.Contains(method) && !context.User.Identity?.IsAuthenticated == true)
        {
            // Build return URL
            var returnUrl = context.Request.Path + context.Request.QueryString;
            var loginUrl = $"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}";

            context.Response.Redirect(loginUrl);
            return;
        }

        await _next(context);
    }

    private static bool IsPublicPath(string path)
    {
        // Allow setup pages (first-run wizard handles its own access)
        if (path.StartsWith("/setup", StringComparison.OrdinalIgnoreCase))
            return true;

        // Allow auth endpoints (avoid redirect loop)
        if (path.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase))
            return true;

        // Allow BFF endpoints to handle their own auth
        if (path.StartsWith("/bff/", StringComparison.OrdinalIgnoreCase))
            return true;

        // Allow static files
        if (path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/avatars/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/favicon.ico", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/robots.txt", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}

public static class AuthRedirectMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthRedirect(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuthRedirectMiddleware>();
    }
}
