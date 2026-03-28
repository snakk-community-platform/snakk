using System.Security.Cryptography;
using System.Text;

namespace Snakk.Realtime.Middleware;

/// <summary>
/// Middleware to validate API key for internal service calls
/// </summary>
public class ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly byte[] _apiKeyBytes = Encoding.UTF8.GetBytes(
        configuration["ApiKey"] ?? throw new InvalidOperationException("ApiKey not configured"));

    public async Task InvokeAsync(HttpContext context)
    {
        // Require API key only for /api/* endpoints (internal service calls).
        // All other paths (SignalR hub at /, negotiate, WebSocket) pass through freely.
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            if (!context.Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey)
                || string.IsNullOrEmpty(extractedApiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "API Key missing" });
                return;
            }

            // Constant-time comparison prevents timing attacks
            var extractedBytes = Encoding.UTF8.GetBytes(extractedApiKey.ToString());
            if (!CryptographicOperations.FixedTimeEquals(extractedBytes, _apiKeyBytes))
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
