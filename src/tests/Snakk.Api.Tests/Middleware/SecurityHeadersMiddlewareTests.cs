using Microsoft.AspNetCore.Http;
using Snakk.Api.Middleware;

namespace Snakk.Api.Tests.Middleware;

public class SecurityHeadersMiddlewareTests
{
    private static async Task<HttpContext> InvokeMiddleware()
    {
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(ctx => Task.CompletedTask);
        await middleware.InvokeAsync(context);
        return context;
    }

    [Test]
    public async Task InvokeAsync_AddsStrictTransportSecurity()
    {
        var context = await InvokeMiddleware();

        var header = context.Response.Headers["Strict-Transport-Security"].ToString();
        await Assert.That(header).IsEqualTo("max-age=31536000; includeSubDomains");
    }

    [Test]
    public async Task InvokeAsync_AddsXFrameOptionsDeny()
    {
        var context = await InvokeMiddleware();

        var header = context.Response.Headers["X-Frame-Options"].ToString();
        await Assert.That(header).IsEqualTo("DENY");
    }

    [Test]
    public async Task InvokeAsync_AddsXContentTypeOptionsNosniff()
    {
        var context = await InvokeMiddleware();

        var header = context.Response.Headers["X-Content-Type-Options"].ToString();
        await Assert.That(header).IsEqualTo("nosniff");
    }

    [Test]
    public async Task InvokeAsync_AddsXXssProtection()
    {
        var context = await InvokeMiddleware();

        var header = context.Response.Headers["X-XSS-Protection"].ToString();
        await Assert.That(header).IsEqualTo("1; mode=block");
    }

    [Test]
    public async Task InvokeAsync_AddsReferrerPolicy()
    {
        var context = await InvokeMiddleware();

        var header = context.Response.Headers["Referrer-Policy"].ToString();
        await Assert.That(header).IsEqualTo("strict-origin-when-cross-origin");
    }

    [Test]
    public async Task InvokeAsync_AddsPermissionsPolicy()
    {
        var context = await InvokeMiddleware();

        var header = context.Response.Headers["Permissions-Policy"].ToString();
        await Assert.That(header).IsEqualTo(
            "geolocation=(), microphone=(), camera=(), payment=(), usb=(), magnetometer=(), gyroscope=()");
    }

    [Test]
    public async Task InvokeAsync_AddsContentSecurityPolicy()
    {
        var context = await InvokeMiddleware();

        var header = context.Response.Headers["Content-Security-Policy"].ToString();
        await Assert.That(header).Contains("default-src 'self'");
        await Assert.That(header).Contains("script-src 'self'");
        await Assert.That(header).Contains("frame-ancestors 'none'");
        await Assert.That(header).Contains("base-uri 'self'");
    }

    [Test]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        var nextCalled = false;
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        await Assert.That(nextCalled).IsTrue();
    }
}
