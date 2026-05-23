using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Snakk.Api.Tests.Helpers;
using Snakk.Infrastructure.Database;

namespace Snakk.Api.Tests.Endpoints;

public class AuthEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Test]
    public async Task Register_WithValidData_Returns_200_With_Tokens()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new
        {
            email = "newuser@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "NewUser"
        };

        // Act
        var response = await client.PostAsJsonAsync("/auth/register", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("accessToken", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("refreshToken", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("user", out _)).IsTrue();
    }

    [Test]
    public async Task Register_WithMissingEmail_Returns_BadRequest()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new
        {
            email = "",
            password = "StrongP@ssw0rd!",
            displayName = "NewUser"
        };

        // Act
        var response = await client.PostAsJsonAsync("/auth/register", request);

        // Assert
        // Registration with empty email should fail (either 400 or validation error)
        await Assert.That((int)response.StatusCode).IsGreaterThanOrEqualTo(400);
    }

    [Test]
    public async Task Login_WithInvalidCredentials_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new
        {
            email = "nonexistent@example.com",
            password = "WrongPassword123!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/auth/login", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Login_AfterRegistration_Returns_200_With_Tokens()
    {
        // Arrange
        var client = _server.CreateClient();

        // First, register a user
        var registerRequest = new
        {
            email = "logintest@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "LoginTestUser"
        };
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Act - login with the same credentials
        var loginRequest = new
        {
            email = "logintest@example.com",
            password = "StrongP@ssw0rd!"
        };
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("accessToken", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("refreshToken", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("user", out _)).IsTrue();
    }

    [Test]
    public async Task AuthStatus_WhenUnauthenticated_Returns_IsAuthenticated_False()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/auth/status");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("isAuthenticated").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task AuthStatus_WhenAuthenticated_Returns_IsAuthenticated_True()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(
            userId: "user-123",
            displayName: "Auth Test User",
            email: "authtest@example.com");

        // Act
        var response = await client.GetAsync("/auth/status");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("isAuthenticated").GetBoolean()).IsTrue();
        await Assert.That(json.RootElement.GetProperty("publicId").GetString()).IsEqualTo("user-123");
        await Assert.That(json.RootElement.GetProperty("displayName").GetString()).IsEqualTo("Auth Test User");
    }

    [Test]
    public async Task GetCurrentUser_WhenUnauthenticated_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/me");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetCurrentUser_WhenAuthenticated_WithRegisteredUser_Returns_UserDetails()
    {
        // Arrange
        var client = _server.CreateClient();

        // Register a user first to have them in the database
        var registerRequest = new
        {
            email = "metest@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "MeTestUser"
        };
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Extract the user ID from the registration response
        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        var registerJson = JsonDocument.Parse(registerContent);
        var userId = registerJson.RootElement.GetProperty("user").GetProperty("id").GetString()!;

        // Create an authenticated client with the user's actual ID
        var authenticatedClient = _server.CreateAuthenticatedClient(
            userId: userId,
            displayName: "MeTestUser",
            email: "metest@example.com");

        // Act
        var response = await authenticatedClient.GetAsync("/me");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("displayName").GetString()).IsEqualTo("MeTestUser");
        await Assert.That(json.RootElement.GetProperty("email").GetString()).IsEqualTo("metest@example.com");
    }

    [Test]
    public async Task AuthStatus_HasNoCacheHeaders()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/auth/status");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var cacheControl = response.Headers.CacheControl;
        await Assert.That(cacheControl).IsNotNull();
        await Assert.That(cacheControl!.NoStore).IsTrue();
        await Assert.That(cacheControl!.NoCache).IsTrue();
    }

    // Regression: prior to fix/rest-2fa-bypass-paths, /auth/login on the REST surface
    // ignored TwoFactorEnabled and issued a full access+refresh pair on the strength of
    // (email, password) alone — turning 2FA off for any client that bypassed Snakk.Auth
    // (which uses gRPC) and called the REST endpoint directly. Login now mirrors the
    // gRPC Login contract: when 2FA is on, no session is issued; the caller receives a
    // short-lived pending token and must complete /auth/2fa/verify.
    [Test]
    public async Task Login_When2FAEnabled_Returns_RequiresTwoFactor_NoSessionTokens()
    {
        // Arrange — register a user, then flip TwoFactorEnabled on the in-memory DB.
        var client = _server.CreateClient();
        var registerRequest = new
        {
            email = "login2fa@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "Login2FAUser"
        };
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var registerJson = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        var userPublicId = registerJson.RootElement.GetProperty("user").GetProperty("id").GetString()!;

        // Direct DB mutation: register stores Email encrypted, so we look up by PublicId.
        using (var scope = _server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();
            var user = await db.Users.FirstAsync(u => u.PublicId == userPublicId);
            user.TwoFactorEnabled = true;
            await db.SaveChangesAsync();
        }

        // Act
        var loginRequest = new
        {
            email = "login2fa@example.com",
            password = "StrongP@ssw0rd!"
        };
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);

        // Assert — 200 with requiresTwoFactor=true; no access/refresh token issued
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("requiresTwoFactor").GetBoolean()).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("twoFactorPendingToken", out var pendingToken)).IsTrue();
        await Assert.That(string.IsNullOrEmpty(pendingToken.GetString())).IsFalse();

        // The contract change is the whole point: no session credentials must leak here.
        await Assert.That(json.RootElement.TryGetProperty("accessToken", out _)).IsFalse();
        await Assert.That(json.RootElement.TryGetProperty("refreshToken", out _)).IsFalse();
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
