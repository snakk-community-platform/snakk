namespace Snakk.Api.Middleware;

/// <summary>
/// Middleware that adds security headers to all HTTP responses
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // HSTS - Force HTTPS for 1 year, include subdomains
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

        // X-Frame-Options - Prevent clickjacking
        context.Response.Headers.Append("X-Frame-Options", "DENY");

        // X-Content-Type-Options - Prevent MIME-sniffing
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // X-XSS-Protection - Enable browser XSS filter (legacy but still useful)
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

        // Referrer-Policy - Control referrer information
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // Permissions-Policy - Disable unnecessary browser features
        context.Response.Headers.Append(
            "Permissions-Policy",
            "geolocation=(), microphone=(), camera=(), payment=(), usb=(), magnetometer=(), gyroscope=()");

        // Content-Security-Policy - Strict API policy (no HTML/scripts served)
        var csp = string.Join("; ", new[]
        {
            "default-src 'none'",
            "frame-ancestors 'none'",
            "base-uri 'none'",
            "form-action 'none'"
        });
        context.Response.Headers.Append("Content-Security-Policy", csp);
        context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");

        await next(context);
    }
}

/// <summary>
/// Extension methods for registering SecurityHeadersMiddleware
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder) =>
        builder.UseMiddleware<SecurityHeadersMiddleware>();
}
