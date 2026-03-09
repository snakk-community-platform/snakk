using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Snakk.Api.Tests.Helpers;

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
            displayName = "New User"
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
            displayName = "New User"
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
            displayName = "Login Test User"
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
            displayName = "Me Test User"
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
            displayName: "Me Test User",
            email: "metest@example.com");

        // Act
        var response = await authenticatedClient.GetAsync("/me");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("displayName").GetString()).IsEqualTo("Me Test User");
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

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
