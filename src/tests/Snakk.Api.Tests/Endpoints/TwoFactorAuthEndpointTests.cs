using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class TwoFactorAuthEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    // ──────────────────────────────────────────────
    // POST /api/auth/2fa/setup
    // ──────────────────────────────────────────────

    [Test]
    public async Task Setup2FA_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.PostAsync("/auth/2fa/setup", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Setup2FA_WithAuth_ReturnsOkWithSecretAndQrUri()
    {
        // Arrange — register a user so ITwoFactorAuthService can find them in the DB
        var client = _server.CreateClient();
        var registerRequest = new
        {
            email = "setup2fa@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "Setup2FAUser"
        };
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        var registerJson = JsonDocument.Parse(registerContent);
        var userId = registerJson.RootElement.GetProperty("user").GetProperty("id").GetString()!;

        var authClient = _server.CreateAuthenticatedClient(
            userId: userId,
            displayName: "Setup2FAUser",
            email: "setup2fa@example.com");

        // Act
        var response = await authClient.PostAsync("/auth/2fa/setup", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("secret", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("qrCodeUri", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("message", out _)).IsTrue();
    }

    // ──────────────────────────────────────────────
    // POST /api/auth/2fa/enable
    // ──────────────────────────────────────────────

    [Test]
    public async Task Enable2FA_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new { code = "123456" };

        // Act
        var response = await client.PostAsJsonAsync("/auth/2fa/enable", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Enable2FA_WithAuth_InvalidCode_ReturnsBadRequest()
    {
        // Arrange — register a user and set up 2FA first so the enable path has a secret
        var client = _server.CreateClient();
        var registerRequest = new
        {
            email = "enable2fa@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "Enable2FAUser"
        };
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        var registerJson = JsonDocument.Parse(registerContent);
        var userId = registerJson.RootElement.GetProperty("user").GetProperty("id").GetString()!;

        var authClient = _server.CreateAuthenticatedClient(
            userId: userId,
            displayName: "Enable2FAUser",
            email: "enable2fa@example.com");

        // Set up 2FA so a secret exists
        await authClient.PostAsync("/auth/2fa/setup", null);

        // Act — try enabling with a bogus code
        var enableRequest = new { code = "000000" };
        var response = await authClient.PostAsJsonAsync("/auth/2fa/enable", enableRequest);

        // Assert — invalid TOTP code should be rejected
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────
    // POST /api/auth/2fa/disable
    // ──────────────────────────────────────────────

    [Test]
    public async Task Disable2FA_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new { password = "SomePassword!" };

        // Act
        var response = await client.PostAsJsonAsync("/auth/2fa/disable", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Disable2FA_WithAuth_WhenNotEnabled_ReturnsBadRequest()
    {
        // Arrange — register a user but never enable 2FA
        var client = _server.CreateClient();
        var registerRequest = new
        {
            email = "disable2fa@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "Disable2FAUser"
        };
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        var registerJson = JsonDocument.Parse(registerContent);
        var userId = registerJson.RootElement.GetProperty("user").GetProperty("id").GetString()!;

        var authClient = _server.CreateAuthenticatedClient(
            userId: userId,
            displayName: "Disable2FAUser",
            email: "disable2fa@example.com");

        // Act — try disabling 2FA when it was never enabled
        var disableRequest = new { password = "StrongP@ssw0rd!" };
        var response = await authClient.PostAsJsonAsync("/auth/2fa/disable", disableRequest);

        // Assert — should fail because 2FA is not enabled
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // Regression: a body without a sudo token must be rejected before any other check.
    // Prior to fix/rest-2fa-bypass-paths, POST /auth/2fa/disable accepted an empty body
    // (or any body, ignored) and disabled 2FA on the strength of the access token alone.
    [Test]
    public async Task Disable2FA_WithoutSudoToken_ReturnsBadRequest()
    {
        // Arrange
        var client = _server.CreateClient();
        var registerRequest = new
        {
            email = "disable2fa-nosudo@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "Disable2FANoSudo"
        };
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var registerJson = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        var userId = registerJson.RootElement.GetProperty("user").GetProperty("id").GetString()!;

        var authClient = _server.CreateAuthenticatedClient(
            userId: userId,
            displayName: "Disable2FANoSudo",
            email: "disable2fa-nosudo@example.com");

        // Act — empty body, no sudo token
        var response = await authClient.PostAsJsonAsync("/auth/2fa/disable", new { });

        // Assert — must be rejected for missing sudo, NOT silently disable 2FA
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Sudo");
    }

    // Regression: an invalid sudo token must be rejected with 403, not accepted.
    [Test]
    public async Task Disable2FA_WithInvalidSudoToken_ReturnsForbidden()
    {
        var client = _server.CreateClient();
        var registerRequest = new
        {
            email = "disable2fa-badsudo@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "Disable2FABadSudo"
        };
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var registerJson = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        var userId = registerJson.RootElement.GetProperty("user").GetProperty("id").GetString()!;

        var authClient = _server.CreateAuthenticatedClient(
            userId: userId,
            displayName: "Disable2FABadSudo",
            email: "disable2fa-badsudo@example.com");

        var response = await authClient.PostAsJsonAsync(
            "/auth/2fa/disable", new { sudoToken = "not-a-real-sudo-token" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────
    // POST /api/auth/2fa/verify
    // ──────────────────────────────────────────────

    // Regression: prior to fix/rest-2fa-bypass-paths, /auth/2fa/verify was AllowAnonymous
    // and looked up the user purely by email — turning 2FA into the only required factor
    // for any account with 2FA enabled. The endpoint now requires a pending token issued
    // by /auth/login, which proves the password step already cleared.
    [Test]
    public async Task Verify2FA_WithoutPendingToken_ReturnsBadRequest()
    {
        var client = _server.CreateClient();
        var request = new { code = "123456" };

        var response = await client.PostAsJsonAsync("/auth/2fa/verify", request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Verify2FA_WithInvalidPendingToken_ReturnsBadRequest()
    {
        var client = _server.CreateClient();
        var request = new { code = "123456", twoFactorPendingToken = "not-a-real-jwt" };

        var response = await client.PostAsJsonAsync("/auth/2fa/verify", request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // Even a structurally-valid JWT signed with the right key is rejected if it lacks
    // the 2fa-pending audience/purpose. This stops an attacker from presenting a
    // captured access token as a pending token.
    [Test]
    public async Task Verify2FA_WithAccessTokenAsPendingToken_ReturnsBadRequest()
    {
        var client = _server.CreateClient();
        var accessToken = AuthHelper.GenerateTestToken(
            userId: "some-user", displayName: "X", email: "x@example.com", emailVerified: true);

        var request = new { code = "123456", twoFactorPendingToken = accessToken };

        var response = await client.PostAsJsonAsync("/auth/2fa/verify", request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────
    // GET /api/auth/2fa/backup-codes
    // ──────────────────────────────────────────────

    [Test]
    public async Task GetBackupCodes_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/auth/2fa/backup-codes");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetBackupCodes_WithAuth_ReturnsOkOrBadRequest()
    {
        // Arrange — register a user (2FA not enabled, so service may return error)
        var client = _server.CreateClient();
        var registerRequest = new
        {
            email = "backupcodes@example.com",
            password = "StrongP@ssw0rd!",
            displayName = "BackupCodesUser"
        };
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        var registerJson = JsonDocument.Parse(registerContent);
        var userId = registerJson.RootElement.GetProperty("user").GetProperty("id").GetString()!;

        var authClient = _server.CreateAuthenticatedClient(
            userId: userId,
            displayName: "BackupCodesUser",
            email: "backupcodes@example.com");

        // Act
        var response = await authClient.GetAsync("/auth/2fa/backup-codes");

        // Assert — should be 200 (with status) or 400 (if 2FA not enabled), but NOT 401
        await Assert.That((int)response.StatusCode).IsNotEqualTo((int)HttpStatusCode.Unauthorized);
        await Assert.That(
            response.StatusCode == HttpStatusCode.OK
            || response.StatusCode == HttpStatusCode.BadRequest
        ).IsTrue();
    }

    // ──────────────────────────────────────────────
    // POST /api/auth/2fa/backup-codes/regenerate
    // ──────────────────────────────────────────────

    [Test]
    public async Task RegenerateBackupCodes_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new { password = "SomePassword!" };

        // Act
        var response = await client.PostAsJsonAsync("/auth/2fa/backup-codes/regenerate", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────
    // POST /api/auth/2fa/trust-device
    // ──────────────────────────────────────────────

    [Test]
    public async Task TrustDevice_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new { expirationDays = 30 };

        // Act
        var response = await client.PostAsJsonAsync("/auth/2fa/trust-device", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────
    // GET /api/auth/2fa/trusted-devices
    // ──────────────────────────────────────────────

    [Test]
    public async Task GetTrustedDevices_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/auth/2fa/trusted-devices");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────
    // DELETE /api/auth/2fa/trusted-devices/{deviceId}
    // ──────────────────────────────────────────────

    [Test]
    public async Task RevokeTrustedDevice_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.DeleteAsync("/auth/2fa/trusted-devices/some-device-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────
    // Cleanup
    // ──────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
