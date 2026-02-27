using System.Net;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class HealthCheckTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    [Test]
    public async Task Health_Endpoint_Returns_200_OK()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task SecurityTxt_Endpoint_Returns_200_With_PlainText()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/.well-known/security.txt");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).Contains("Contact:");
        await Assert.That(content).Contains("Expires:");
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
