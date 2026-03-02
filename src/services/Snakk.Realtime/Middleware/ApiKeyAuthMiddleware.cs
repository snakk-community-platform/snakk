namespace Snakk.Realtime.Middleware;

/// <summary>
/// Middleware to validate API key for internal service calls
/// </summary>
public class ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly string _apiKey = configuration["ApiKey"]
        ?? throw new InvalidOperationException("ApiKey not configured");

    public async Task InvokeAsync(HttpContext context)
    {
        // Require API key only for /api/* endpoints (internal service calls).
        // All other paths (SignalR hub at /, negotiate, WebSocket) pass through freely.
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            if (!context.Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "API Key missing" });
                return;
            }

            if (extractedApiKey != _apiKey)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid API Key" });
                return;
            }
        }

        await next(context);
    }
}

public static class ApiKeyAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder builder) =>
        builder.UseMiddleware<ApiKeyAuthMiddleware>();
}
