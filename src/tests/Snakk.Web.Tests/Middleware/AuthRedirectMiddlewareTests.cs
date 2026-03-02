using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Snakk.Web.Middleware;

namespace Snakk.Web.Tests.Middleware;

/// <summary>
/// Tests for AuthRedirectMiddleware which redirects unauthenticated users to login
/// when attempting modifying requests (POST, PUT, DELETE, PATCH).
/// GET requests and public paths always pass through.
/// </summary>
public class AuthRedirectMiddlewareTests
{
    private static AuthRedirectMiddleware CreateMiddleware(out Func<bool> wasNextCalled)
    {
        var called = false;
        wasNextCalled = () => called;
        RequestDelegate next = _ =>
        {
            called = true;
            return Task.CompletedTask;
        };

        return new AuthRedirectMiddleware(next);
    }

    private static DefaultHttpContext CreateHttpContext(
        string method = "GET",
        string path = "/some/path",
        bool authenticated = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        if (authenticated)
        {
            var claims = new[] { new Claim("sub", "user-001") };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            context.User = new ClaimsPrincipal(identity);
        }

        return context;
    }

    // ===== GET Request Tests =====

    [Test]
    public async Task GetRequest_Unauthenticated_PassesThrough()
    {
        var middleware = CreateMiddleware(out var wasNextCalled);
        var context = CreateHttpContext(method: "GET", authenticated: false);

        await middleware.InvokeAsync(context);

        await Assert.That(wasNextCalled()).IsTrue();
    }

    [Test]
    public async Task GetRequest_Authenticated_PassesThrough()
    {
        var middleware = CreateMiddleware(out var wasNextCalled);
        var context = CreateHttpContext(method: "GET", authenticated: true);

        await middleware.InvokeAsync(context);

        await Assert.That(wasNextCalled()).IsTrue();
    }

    // ===== Modifying Request Tests (Unauthenticated) =====

    [Test]
    public async Task PostRequest_Unauthenticated_RedirectsToLogin()
    {
        var middleware = CreateMiddleware(out var wasNextCalled);
        var context = CreateHttpContext(method: "POST", authenticated: false);

        await middleware.InvokeAsync(context);

        await Assert.That(wasNextCalled()).IsFalse();
        await Assert.That(context.Response.StatusCode).IsEqualTo(302);
    }

    [Test]
    public async Task PutRequest_Unauthenticated_RedirectsToLogin()
    {
        var middleware = CreateMiddleware(out var wasNextCalled);
        var context = CreateHttpContext(method: "PUT", authenticated: false);

        await middleware.InvokeAsync(context);

        await Assert.That(wasNextCalled()).IsFalse();
        await Assert.That(context.Response.StatusCode).IsEqualTo(302);
    }

    [Test]
    public async Task DeleteRequest_Unauthenticated_RedirectsToLogin()
    {
        var middleware = CreateMiddleware(out var wasNextCalled);
        var context = CreateHttpContext(method: "DELETE", authenticated: false);

        await middleware.InvokeAsync(context);

        await Assert.That(wasNextCalled()).IsFalse();
        await Assert.That(context.Response.StatusCode).IsEqualTo(302);
    }

    [Test]
    public async Task PatchRequest_Unauthenticated_RedirectsToLogin()
    {
        var middleware = CreateMiddleware(out var wasNextCalled);
        var context = CreateHttpContext(method: "PATCH", authenticated: false);

        await middleware.InvokeAsync(context);

        await Assert.That(wasNextCalled()).IsFalse();
        await Assert.That(context.Response.StatusCode).IsEqualTo(302);
    }

    // ===== Modifying Request Tests (Authenticated) =====

    [Test]
    public async Task PostRequest_Authenticated_PassesThrough()
    {
        var middleware = CreateMiddleware(out var wasNextCalled);
        var context = CreateHttpContext(method: "POST", authenticated: true);

        await middleware.InvokeAsync(context);

        await Assert.That(wasNextCalled()).IsTrue();
    }

    // ===== Return URL Tests =====

    [Test]
    public async Task PostRequest_Unauthenticated_IncludesReturnUrl()
    {
        var middleware = CreateMiddleware(out _);
        var context = CreateHttpContext(method: "POST", path: "/discussions/create", authenticated: false);
        context.Request.QueryString = new QueryString("?space=tech");

        await middleware.InvokeAsync(context);

        var location = context.Response.Headers.Location.ToString();
        await Assert.That(location).Contains("/auth/login?returnUrl=");
        await Assert.That(location).Contains(Uri.EscapeDataString("/discussions/create?space=tech"));
    }

    // ===== Public Path Tests =====

    [Test]
    public async Task PublicPath_Auth_PassesThrough()
    {
        var middleware = CreateMiddleware(out var wasNextCalled);
        var context = CreateHttpContext(method: "POST", path: "/auth/login", authenticated: false);

        await middleware.InvokeAsync(context);

        await Assert.That(wasNextCalled()).IsTrue();
    }

    [Test]
    public async Task PublicPath_Bff_PassesThrough()
    {
        var middleware = CreateMiddleware(out var wasNextCalled);
        var context = CreateHttpContext(method: "POST", path: "/bff/data", authenticated: false);

        await middleware.InvokeAsync(context);

        await Assert.That(wasNextCalled()).IsTrue();
    }

    [Test]
    public async Task PublicPath_Css_PassesThrough()
    {
        var middleware = CreateMiddleware(out var wasNextCalled);
        var context = CreateHttpContext(method: "POST", path: "/css/style.css", authenticated: false);

        await middleware.InvokeAsync(context);

        await Assert.That(wasNextCalled()).IsTrue();
    }

    [Test]
    public async Task PublicPath_Setup_PassesThrough()
    {
        var middleware = CreateMiddleware(out var wasNextCalled);
        var context = CreateHttpContext(method: "POST", path: "/setup", authenticated: false);

        await middleware.InvokeAsync(context);

        await Assert.That(wasNextCalled()).IsTrue();
    }
}
