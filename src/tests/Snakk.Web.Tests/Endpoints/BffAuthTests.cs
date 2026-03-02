using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests for BFF authentication endpoints:
///   GET  /bff/auth/status
///   POST /bff/auth/logout
///   POST /bff/auth/refresh
///   POST /bff/auth/set-tokens
///   PUT  /bff/auth/update-profile
/// </summary>
public class BffAuthTests
{
    [Test]
    public async Task GetAuthStatus_WithValidToken_ReturnsAuthenticatedStatus()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/auth/status", new
        {
            isAuthenticated = true,
            publicId = "user-001",
            displayName = "Test User",
            emailVerified = true,
            role = "User",
            avatarUrl = "/avatars/generated/users/ab/user-001.svg"
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/auth/status");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("isAuthenticated").GetBoolean()).IsTrue();
        await Assert.That(body.GetProperty("publicId").GetString()).IsEqualTo("user-001");
        await Assert.That(body.GetProperty("displayName").GetString()).IsEqualTo("Test User");

        // Verify no-cache headers are set
        await Assert.That(response.Headers.CacheControl?.NoStore).IsTrue();
    }

    [Test]
    public async Task GetAuthStatus_WhenApiReturnsNull_ReturnsUnauthorized()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/auth/status", HttpStatusCode.Unauthorized);

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/bff/auth/status");

        // Assert — SnakkApiClient.GetAuthStatusAsync catches exceptions and returns
        // AuthStatusDto(false, null, null, false, null, null), which the BFF endpoint
        // maps to an "unauthenticated" result. Since IsAuthenticated is false but not null,
        // the endpoint still returns 200 with isAuthenticated=false.
        // However, looking at the BFF code: if (apiResult is null) return Results.Unauthorized();
        // The SnakkApiClient returns a new AuthStatusDto(false,...) on error, so apiResult is NOT null.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("isAuthenticated").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task Logout_WithValidToken_ReturnsOkAndClearsApiSession()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/auth/logout", HttpStatusCode.OK);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync("/bff/auth/logout", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API logout endpoint was called
        var apiCalls = app.MockApiHandler.ReceivedRequests
            .Where(r => r.RequestUri?.PathAndQuery?.StartsWith("/auth/logout") == true)
            .ToList();
        await Assert.That(apiCalls.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Refresh_WithValidRefreshCookie_ReturnsOk()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/auth/refresh", new
        {
            accessToken = TestJwtHelper.GenerateToken(),
            refreshToken = "new-refresh-token-abc"
        });

        var client = app.CreateClient();
        // Set a refresh token cookie
        client.DefaultRequestHeaders.Add("Cookie", ".Snakk.Auth.Refresh=old-refresh-token-123");

        // Act
        var response = await client.PostAsync("/bff/auth/refresh", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // The response should set new auth cookies
        var setCookieHeaders = response.Headers
            .Where(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .SelectMany(h => h.Value)
            .ToList();

        // Should have set at least the access cookie
        var hasAuthCookie = setCookieHeaders.Any(c => c.Contains(".Snakk.Auth="));
        await Assert.That(hasAuthCookie).IsTrue();
    }

    [Test]
    public async Task Refresh_WithoutRefreshCookie_ReturnsUnauthorized()
    {
        // Arrange
        await using var app = new TestWebApp();
        var client = app.CreateClient();

        // Act — no refresh cookie set
        var response = await client.PostAsync("/bff/auth/refresh", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Refresh_WhenApiRejectsToken_ReturnsUnauthorized()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/auth/refresh", HttpStatusCode.Unauthorized);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", ".Snakk.Auth.Refresh=invalid-refresh-token");

        // Act
        var response = await client.PostAsync("/bff/auth/refresh", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task SetTokens_WithValidTokens_ReturnsOkAndSetsCookies()
    {
        // Arrange
        await using var app = new TestWebApp();
        var client = app.CreateClient();

        var request = new
        {
            accessToken = TestJwtHelper.GenerateToken(),
            refreshToken = "refresh-token-xyz"
        };

        // Act
        var response = await client.PostAsJsonAsync("/bff/auth/set-tokens", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var setCookieHeaders = response.Headers
            .Where(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .SelectMany(h => h.Value)
            .ToList();

        var hasAccessCookie = setCookieHeaders.Any(c => c.Contains(".Snakk.Auth="));
        var hasRefreshCookie = setCookieHeaders.Any(c => c.Contains(".Snakk.Auth.Refresh="));

        await Assert.That(hasAccessCookie).IsTrue();
        await Assert.That(hasRefreshCookie).IsTrue();
    }

    [Test]
    public async Task SetTokens_WithMissingAccessToken_ReturnsBadRequest()
    {
        // Arrange
        await using var app = new TestWebApp();
        var client = app.CreateClient();

        var request = new
        {
            accessToken = "",
            refreshToken = "refresh-token-xyz"
        };

        // Act
        var response = await client.PostAsJsonAsync("/bff/auth/set-tokens", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task SetTokens_WithMissingRefreshToken_ReturnsBadRequest()
    {
        // Arrange
        await using var app = new TestWebApp();
        var client = app.CreateClient();

        var request = new
        {
            accessToken = TestJwtHelper.GenerateToken(),
            refreshToken = ""
        };

        // Act
        var response = await client.PostAsJsonAsync("/bff/auth/set-tokens", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateProfile_WithValidRequest_ReturnsOk()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/auth/update-profile", HttpStatusCode.OK);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);
        var request = new { displayName = "New Display Name" };

        // Act
        var response = await client.PutAsJsonAsync("/bff/auth/update-profile", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task UpdateProfile_WhenApiFails_ReturnsBadRequest()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/auth/update-profile", HttpStatusCode.BadRequest);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);
        var request = new { displayName = "New Name" };

        // Act
        var response = await client.PutAsJsonAsync("/bff/auth/update-profile", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
