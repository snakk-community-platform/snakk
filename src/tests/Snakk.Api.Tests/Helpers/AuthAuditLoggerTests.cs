using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Snakk.Api.Helpers;

namespace Snakk.Api.Tests.Helpers;

public class AuthAuditLoggerTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Test]
    public async Task LogLoginSuccess_DoesNotThrow()
    {
        await Assert.That(() =>
            AuthAuditLogger.LogLoginSuccess(_logger, "test@test.com", "127.0.0.1", "TestAgent"))
            .ThrowsNothing();
    }

    [Test]
    public async Task LogLoginFailure_DoesNotThrow()
    {
        await Assert.That(() =>
            AuthAuditLogger.LogLoginFailure(_logger, "test@test.com", "127.0.0.1", "TestAgent"))
            .ThrowsNothing();
    }

    [Test]
    public async Task LogRegistration_DoesNotThrow()
    {
        await Assert.That(() =>
            AuthAuditLogger.LogRegistration(_logger, "test@test.com", "127.0.0.1", "TestAgent"))
            .ThrowsNothing();
    }

    [Test]
    public async Task LogOAuthLogin_DoesNotThrow()
    {
        await Assert.That(() =>
            AuthAuditLogger.LogOAuthLogin(_logger, "Google", "test@test.com", "127.0.0.1", "TestAgent"))
            .ThrowsNothing();
    }

    [Test]
    public async Task LogLogout_DoesNotThrow()
    {
        await Assert.That(() =>
            AuthAuditLogger.LogLogout(_logger, "user-1", "127.0.0.1", "TestAgent"))
            .ThrowsNothing();
    }

    [Test]
    public async Task GetClientIp_WithXForwardedFor_ReturnsFirstIp()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.1, 198.51.100.1";

        var ip = AuthAuditLogger.GetClientIp(context);

        await Assert.That(ip).IsEqualTo("203.0.113.1");
    }

    [Test]
    public async Task GetClientIp_WithSingleForwardedFor_ReturnsIp()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "10.0.0.1";

        var ip = AuthAuditLogger.GetClientIp(context);

        await Assert.That(ip).IsEqualTo("10.0.0.1");
    }

    [Test]
    public async Task GetClientIp_NoHeader_ReturnsRemoteIp()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        var ip = AuthAuditLogger.GetClientIp(context);

        await Assert.That(ip).IsEqualTo("192.168.1.1");
    }

    [Test]
    public async Task GetClientIp_NoHeaderNoRemoteIp_ReturnsUnknown()
    {
        var context = new DefaultHttpContext();
        // DefaultHttpContext has no RemoteIpAddress by default

        var ip = AuthAuditLogger.GetClientIp(context);

        await Assert.That(ip).IsEqualTo("unknown");
    }

    [Test]
    public async Task GetUserAgent_ReturnsHeaderValue()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["User-Agent"] = "Mozilla/5.0 TestBrowser";

        var userAgent = AuthAuditLogger.GetUserAgent(context);

        await Assert.That(userAgent).IsEqualTo("Mozilla/5.0 TestBrowser");
    }

    [Test]
    public async Task GetUserAgent_NoHeader_ReturnsNull()
    {
        var context = new DefaultHttpContext();

        var userAgent = AuthAuditLogger.GetUserAgent(context);

        await Assert.That(userAgent).IsNull();
    }
}
