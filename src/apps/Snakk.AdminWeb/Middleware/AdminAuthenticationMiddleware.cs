namespace Snakk.AdminWeb.Middleware;

public class AdminAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AdminAuthenticationMiddleware> _logger;

    // Paths that don't require authentication
    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/auth/login",
        "/auth/oauthchallenge",
        "/auth/oauthcomplete",
        "/error"
    };

    public AdminAuthenticationMiddleware(RequestDelegate next, ILogger<AdminAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Skip static files
        if (path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/lib") || path.StartsWith("/favicon"))
        {
            await _next(context);
            return;
        }

        // Skip public paths
        if (PublicPaths.Contains(path))
        {
            await _next(context);
            return;
        }

        // Check if user is authenticated
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            // Redirect to login
            context.Response.Redirect("/Auth/Login?returnUrl=" + Uri.EscapeDataString(path));
            return;
        }

        await _next(context);
    }
}
