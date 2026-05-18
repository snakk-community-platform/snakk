using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using NSubstitute;
using Snakk.Protos.Auth;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests for the BFF realtime token endpoint:
///   GET /bff/realtime-token
/// </summary>
public class BffRealtimeTokenTests
{
    private const string TestRealtimeKey = "TestRealtimeJwtKeyForSnakkWebTests_AtLeast32CharsLong!!";

    [Test]
    public async Task GetRealtimeToken_Unauthenticated_Returns401()
    {
        // Arrange
        await using var app = new TestWebAppWithRealtimeKey();
        var client = app.CreateClient(); // no auth cookie

        // Act
        var response = await client.GetAsync("/bff/realtime-token");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetRealtimeToken_Authenticated_Returns200WithToken()
    {
        // Arrange
        await using var app = new TestWebAppWithRealtimeKey();
        app.MockApiClient.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns((CurrentUserResponse?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/realtime-token");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("token").GetString()).IsNotNull();
        await Assert.That(body.GetProperty("expiresInSeconds").GetInt32()).IsEqualTo(3600);
    }

    [Test]
    public async Task GetRealtimeToken_Authenticated_ReturnValidJwtFormat()
    {
        // Arrange
        await using var app = new TestWebAppWithRealtimeKey();
        app.MockApiClient.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns((CurrentUserResponse?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/realtime-token");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var tokenString = body.GetProperty("token").GetString()!;

        // Assert — JWT is three Base64url segments separated by dots
        var parts = tokenString.Split('.');
        await Assert.That(parts.Length).IsEqualTo(3);
    }

    [Test]
    public async Task GetRealtimeToken_Authenticated_TokenAudienceIsSnakkRealtime()
    {
        // Arrange
        await using var app = new TestWebAppWithRealtimeKey();
        app.MockApiClient.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns((CurrentUserResponse?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app, userId: "test-user-001");

        // Act
        var response = await client.GetAsync("/bff/realtime-token");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var tokenString = body.GetProperty("token").GetString()!;

        // Parse JWT and check audience
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);
        await Assert.That(jwt.Audiences.FirstOrDefault()).IsEqualTo("Snakk-Realtime");
        await Assert.That(jwt.Subject).IsEqualTo("test-user-001");
    }

    [Test]
    public async Task GetRealtimeToken_Authenticated_CallsGetCurrentUserOnce()
    {
        // Arrange
        await using var app = new TestWebAppWithRealtimeKey();
        app.MockApiClient.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns((CurrentUserResponse?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        await client.GetAsync("/bff/realtime-token");

        // Assert
        await app.MockApiClient.Received(1).GetCurrentUserAsync(Arg.Any<CancellationToken>());
    }
}

/// <summary>
/// TestWebApp subclass that configures the Realtime:JwtKey required by RealtimeTokenEndpoints.
/// </summary>
internal class TestWebAppWithRealtimeKey : TestWebApp
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Realtime:JwtKey", "TestRealtimeJwtKeyForSnakkWebTests_AtLeast32CharsLong!!");
    }
}
