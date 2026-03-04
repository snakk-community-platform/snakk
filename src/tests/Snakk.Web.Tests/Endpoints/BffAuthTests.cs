using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grpc.Core;
using Moq;
using Snakk.Protos.Auth;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests for BFF authentication endpoints:
///   GET  /bff/auth/status
///   POST /bff/auth/logout
///   POST /bff/auth/refresh
///   PUT  /bff/me/profile
/// </summary>
public class BffAuthTests
{
    // ==================== Auth Status ====================

    [Test]
    public async Task GetAuthStatus_WithValidToken_ReturnsAuthenticatedStatus()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .Setup(c => c.GetAuthStatusAsync())
            .ReturnsAsync(new AuthStatusResponse
            {
                IsAuthenticated = true,
                PublicId = "user-001",
                DisplayName = "Test User",
                EmailVerified = true,
                Role = "User",
                AvatarUrl = "/avatars/generated/users/ab/user-001.svg"
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
    public async Task GetAuthStatus_WhenApiReturnsUnauthenticated_ReturnsOkWithFalse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .Setup(c => c.GetAuthStatusAsync())
            .ReturnsAsync(new AuthStatusResponse { IsAuthenticated = false });

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/bff/auth/status");

        // Assert — SnakkApiClient.GetAuthStatusAsync returns a non-null AuthStatusResponse
        // with IsAuthenticated=false on error, so the BFF endpoint returns 200 with the mapped DTO.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("isAuthenticated").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task GetAuthStatus_WhenApiReturnsNull_ReturnsUnauthorized()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .Setup(c => c.GetAuthStatusAsync())
            .ReturnsAsync((AuthStatusResponse?)null);

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/bff/auth/status");

        // Assert — when apiResult is null, the BFF endpoint returns 401
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Logout ====================

    [Test]
    public async Task Logout_WithValidToken_ReturnsOkAndCallsApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .Setup(c => c.LogoutAsync())
            .Returns(Task.CompletedTask);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync("/bff/auth/logout", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API logout was called
        app.MockApiClient.Verify(c => c.LogoutAsync(), Times.Once);
    }

    // ==================== Refresh Token ====================

    [Test]
    public async Task Refresh_WithValidRefreshCookie_ReturnsOkAndSetsCookies()
    {
        // Arrange
        await using var app = new TestWebApp();
        var newAccessToken = TestJwtHelper.GenerateToken();
        var newRefreshToken = "new-refresh-token-abc";

        app.MockAuthClient
            .Setup(c => c.RefreshTokenAsync(
                It.IsAny<RefreshTokenRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(TestGrpcHelpers.CreateAsyncUnaryCall(new RefreshTokenResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            }));

        var client = app.CreateClient();
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
    public async Task Refresh_WhenGrpcReturnsEmptyTokens_ReturnsUnauthorized()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockAuthClient
            .Setup(c => c.RefreshTokenAsync(
                It.IsAny<RefreshTokenRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(TestGrpcHelpers.CreateAsyncUnaryCall(new RefreshTokenResponse
            {
                AccessToken = "",
                RefreshToken = ""
            }));

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", ".Snakk.Auth.Refresh=invalid-refresh-token");

        // Act
        var response = await client.PostAsync("/bff/auth/refresh", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Refresh_WhenGrpcThrows_ReturnsUnauthorized()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockAuthClient
            .Setup(c => c.RefreshTokenAsync(
                It.IsAny<RefreshTokenRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(TestGrpcHelpers.CreateFailedAsyncUnaryCall<RefreshTokenResponse>(
                StatusCode.Unauthenticated, "Token expired"));

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", ".Snakk.Auth.Refresh=expired-refresh-token");

        // Act
        var response = await client.PostAsync("/bff/auth/refresh", null);

        // Assert — the BFF endpoint catches all exceptions and returns 401
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Update Profile ====================

    [Test]
    public async Task UpdateProfile_WithValidRequest_ReturnsOk()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .Setup(c => c.UpdateProfileAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);
        var request = new { displayName = "New Display Name" };

        // Act
        var response = await client.PutAsJsonAsync("/bff/me/profile", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        app.MockApiClient.Verify(c => c.UpdateProfileAsync("New Display Name"), Times.Once);
    }

    [Test]
    public async Task UpdateProfile_WhenApiFails_ReturnsBadRequest()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .Setup(c => c.UpdateProfileAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);
        var request = new { displayName = "New Name" };

        // Act
        var response = await client.PutAsJsonAsync("/bff/me/profile", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
