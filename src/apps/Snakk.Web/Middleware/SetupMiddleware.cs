namespace Snakk.Web.Middleware;

/// <summary>
/// Redirects all requests to /setup if first-run setup has not been completed.
/// Checks for the existence of a .setup-complete marker file in the storage directory.
/// </summary>
public class SetupMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _markerFilePath;
    private bool _setupComplete;

    public SetupMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        var storagePath = configuration["FileStorage:BasePath"] ?? "/app/storage";
        _markerFilePath = Path.Combine(storagePath, ".setup-complete");
        _setupComplete = File.Exists(_markerFilePath);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Cache the check — once setup is complete, skip file system checks
        if (!_setupComplete)
        {
            _setupComplete = File.Exists(_markerFilePath);
        }

        if (_setupComplete)
        {
            // Setup is done — block access to setup pages
            if (context.Request.Path.StartsWithSegments("/setup"))
            {
                context.Response.StatusCode = 404;
                return;
            }

            await _next(context);
            return;
        }

        // Setup not complete — allow setup pages and static assets, redirect everything else
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/setup", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/images", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        context.Response.Redirect("/setup");
    }
}
