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

    // ─────────────────────────────────────────────────────────────────────
    // Regressions for fix/jwt-session-and-authversion-revocation (CR-29 + HI-45)
    // The JwtBearerEvents.OnTokenValidated handler previously checked only the
    // per-jti revocation blacklist. The gRPC AuthValidationInterceptor mirrored
    // session-id (sid), user-revocation (ban), and AuthVersion (password change)
    // checks — REST endpoints did not, so revocation primitives wired into the
    // session-management / moderation / password-change flows were effectively
    // inert on the REST side. These tests cover each of those gates.
    // ─────────────────────────────────────────────────────────────────────

    // CR-29: a session whose id has been revoked via IJwtTokenService.RevokeSession
    // must be rejected on the next REST request. Pre-fix, the cache key was written
    // but no validator read it — stolen tokens survived "Sign out of this session"
    // until natural expiry (up to 8 hours).
    [Test]
    public async Task Authenticated_Request_With_RevokedSession_Returns_Unauthorized()
    {
        // Arrange — register a real user so /me has somebody to find
        var client = _server.CreateClient();
        var registerResponse = await client.PostAsJsonAsync("/auth/register", new
        {
            email = "session-revoke@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "SessionRevokeUser"
        });
        await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var userId = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("user").GetProperty("id").GetString()!;

        const string sessionId = "session-to-revoke";
        var token = AuthHelper.GenerateTestToken(userId: userId, sessionId: sessionId);

        // Revoke the session via the same IJwtTokenService the production SessionManagementService uses.
        using (var scope = _server.Services.CreateScope())
        {
            var jwt = scope.ServiceProvider.GetRequiredService<Snakk.Application.Services.IJwtTokenService>();
            jwt.RevokeSession(sessionId);
        }

        var revokedClient = _server.CreateClient();
        revokedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await revokedClient.GetAsync("/me");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // CR-29 corollary: a token WITHOUT a sid claim (legacy or non-session tokens)
    // should not be rejected by the new check — only the explicit sid revocation
    // path should fire. Ensures we didn't tighten validation past what was intended.
    [Test]
    public async Task Authenticated_Request_With_No_SessionId_Claim_Succeeds()
    {
        var client = _server.CreateClient();
        var registerResponse = await client.PostAsJsonAsync("/auth/register", new
        {
            email = "no-sid@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "NoSidUser"
        });
        var userId = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("user").GetProperty("id").GetString()!;

        // No sessionId — sid claim absent
        var token = AuthHelper.GenerateTestToken(userId: userId);
        var authClient = _server.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await authClient.GetAsync("/me");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    // HI-45: a token whose AuthVersion (ver claim) doesn't match the user's current
    // version must be rejected. The version is bumped by ChangePasswordAsync /
    // CompletePasswordResetAsync — without this check, REST sessions survived
    // password change until natural expiry.
    [Test]
    public async Task Authenticated_Request_With_StaleAuthVersion_Returns_Unauthorized()
    {
        var client = _server.CreateClient();
        var registerResponse = await client.PostAsJsonAsync("/auth/register", new
        {
            email = "authver-stale@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "AuthVerStale"
        });
        var userId = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("user").GetProperty("id").GetString()!;

        // Simulate a password change: current AuthVersion in the cache is 7.
        using (var scope = _server.Services.CreateScope())
        {
            var cache = scope.ServiceProvider.GetRequiredService<Snakk.Application.Services.IAuthVersionCache>();
            await cache.SetAsync(userId, 7);
        }

        // Token was issued under the OLD version (3) — must be rejected.
        var staleToken = AuthHelper.GenerateTestToken(userId: userId, authVersion: 3);
        var staleClient = _server.CreateClient();
        staleClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", staleToken);

        var response = await staleClient.GetAsync("/me");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // HI-45 corollary: a token whose AuthVersion matches the cache passes the new
    // check. Confirms the gate isn't a blanket-reject — only mismatches fail.
    [Test]
    public async Task Authenticated_Request_With_MatchingAuthVersion_Succeeds()
    {
        var client = _server.CreateClient();
        var registerResponse = await client.PostAsJsonAsync("/auth/register", new
        {
            email = "authver-match@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "AuthVerMatch"
        });
        var userId = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("user").GetProperty("id").GetString()!;

        using (var scope = _server.Services.CreateScope())
        {
            var cache = scope.ServiceProvider.GetRequiredService<Snakk.Application.Services.IAuthVersionCache>();
            await cache.SetAsync(userId, 5);
        }

        var freshToken = AuthHelper.GenerateTestToken(userId: userId, authVersion: 5);
        var authClient = _server.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", freshToken);

        var response = await authClient.GetAsync("/me");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    // Parity with the gRPC AuthValidationInterceptor: a user marked revoked via
    // IRevocationCache (set by ModerationUseCase ban flow) must be rejected at
    // REST endpoints too. Pre-fix, banning a user only killed their gRPC calls.
    [Test]
    public async Task Authenticated_Request_With_RevokedUser_Returns_Unauthorized()
    {
        var client = _server.CreateClient();
        var registerResponse = await client.PostAsJsonAsync("/auth/register", new
        {
            email = "user-banned@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "BannedUser"
        });
        var userId = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("user").GetProperty("id").GetString()!;

        using (var scope = _server.Services.CreateScope())
        {
            var revocation = scope.ServiceProvider.GetRequiredService<Snakk.Application.Services.IRevocationCache>();
            await revocation.RevokeUserAsync(userId);
        }

        var token = AuthHelper.GenerateTestToken(userId: userId);
        var authClient = _server.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await authClient.GetAsync("/me");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
