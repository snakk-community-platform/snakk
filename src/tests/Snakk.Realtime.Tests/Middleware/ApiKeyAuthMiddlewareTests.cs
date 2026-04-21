using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Snakk.Realtime.Middleware;
using System.Text.Json;

namespace Snakk.Realtime.Tests.Middleware;

public class ApiKeyAuthMiddlewareTests
{
    private const string ValidApiKey = "test-api-key-12345";

    private static ApiKeyAuthMiddleware CreateMiddleware(
        RequestDelegate? next = null,
        string? apiKey = ValidApiKey)
    {
        var configData = new Dictionary<string, string?>();
        if (apiKey is not null)
            configData["Realtime:ApiKey"] = apiKey;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        next ??= _ => Task.CompletedTask;
        return new ApiKeyAuthMiddleware(next, config);
    }

    private static DefaultHttpContext CreateHttpContext(string path, string? apiKeyHeader = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (apiKeyHeader is not null)
            context.Request.Headers["X-Api-Key"] = apiKeyHeader;

        return context;
    }

    private static async Task<string> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);

        return await reader.ReadToEndAsync();
    }

    #region API Path Tests

    [Test]
    public async Task ApiPath_MissingKey_Returns401()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = CreateHttpContext("/api/broadcast");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        await Assert.That(context.Response.StatusCode).IsEqualTo(401);
    }

    [Test]
    public async Task ApiPath_MissingKey_ReturnsApiKeyMissingErrorMessage()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = CreateHttpContext("/api/broadcast");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var body = await ReadResponseBody(context);
        var json = JsonDocument.Parse(body);
        var error = json.RootElement.GetProperty("error").GetString();
        await Assert.That(error).IsEqualTo("API Key missing");
    }

    [Test]
    public async Task ApiPath_InvalidKey_Returns401()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = CreateHttpContext("/api/broadcast", apiKeyHeader: "wrong-key");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        await Assert.That(context.Response.StatusCode).IsEqualTo(401);
    }

    [Test]
    public async Task ApiPath_InvalidKey_ReturnsInvalidApiKeyErrorMessage()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = CreateHttpContext("/api/broadcast", apiKeyHeader: "wrong-key");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var body = await ReadResponseBody(context);
        var json = JsonDocument.Parse(body);
        var error = json.RootElement.GetProperty("error").GetString();
        await Assert.That(error).IsEqualTo("Invalid API Key");
    }

    [Test]
    public async Task ApiPath_EmptyKey_Returns401()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = CreateHttpContext("/api/broadcast", apiKeyHeader: "");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        await Assert.That(context.Response.StatusCode).IsEqualTo(401);
    }

    [Test]
    public async Task ApiPath_ValidKey_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        var middleware = CreateMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext("/api/broadcast", apiKeyHeader: ValidApiKey);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        await Assert.That(nextCalled).IsTrue();
    }

    [Test]
    public async Task ApiPath_ValidKey_DoesNotReturn401()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = CreateHttpContext("/api/broadcast", apiKeyHeader: ValidApiKey);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        await Assert.That(context.Response.StatusCode).IsNotEqualTo(401);
    }

    #endregion

    #region Non-API Path Tests

    [Test]
    public async Task NonApiPath_NoKey_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        var middleware = CreateMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext("/realtime");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        await Assert.That(nextCalled).IsTrue();
    }

    [Test]
    public async Task NonApiPath_ValidKey_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        var middleware = CreateMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext("/realtime", apiKeyHeader: ValidApiKey);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        await Assert.That(nextCalled).IsTrue();
    }

    [Test]
    public async Task NonApiPath_NoKey_DoesNotReturn401()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = CreateHttpContext("/realtime");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        await Assert.That(context.Response.StatusCode).IsNotEqualTo(401);
    }

    [Test]
    public async Task NonApiPath_RootPath_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        var middleware = CreateMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext("/");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        await Assert.That(nextCalled).IsTrue();
    }

    #endregion

    #region Constructor Tests

    [Test]
    public void Constructor_MissingApiKeyConfig_ThrowsInvalidOperationException()
    {
        // Arrange & Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            CreateMiddleware(apiKey: null);
        });
    }

    #endregion
}
