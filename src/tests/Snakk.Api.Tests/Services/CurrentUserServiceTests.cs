using Microsoft.AspNetCore.Http;
using Snakk.Api.Services;
using System.Security.Claims;

namespace Snakk.Api.Tests.Services;

public class CurrentUserServiceTests
{
    private static CurrentUserService CreateService(ClaimsPrincipal? user = null)
    {
        var context = new DefaultHttpContext();
        if (user is not null)
            context.User = user;

        var accessor = new HttpContextAccessor { HttpContext = context };
        return new CurrentUserService(accessor);
    }

    private static ClaimsPrincipal CreateAuthenticatedUser(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateUnauthenticatedUser()
    {
        var identity = new ClaimsIdentity(); // No authenticationType = unauthenticated
        return new ClaimsPrincipal(identity);
    }

    [Test]
    public async Task GetCurrentUserId_WithClaim_ReturnsUserId()
    {
        var user = CreateAuthenticatedUser(
            new Claim(ClaimTypes.NameIdentifier, "user-123"));
        var service = CreateService(user);

        var userId = service.GetCurrentUserId();
        await Assert.That(userId).IsEqualTo("user-123");
    }

    [Test]
    public async Task GetCurrentUserId_NoClaim_ReturnsNull()
    {
        var user = CreateAuthenticatedUser();
        var service = CreateService(user);

        var userId = service.GetCurrentUserId();
        await Assert.That(userId).IsNull();
    }

    [Test]
    public async Task GetCurrentUserDisplayName_WithClaim_ReturnsName()
    {
        var user = CreateAuthenticatedUser(
            new Claim(ClaimTypes.Name, "TestUser"));
        var service = CreateService(user);

        var displayName = service.GetCurrentUserDisplayName();
        await Assert.That(displayName).IsEqualTo("TestUser");
    }

    [Test]
    public async Task GetCurrentUserEmail_WithClaim_ReturnsEmail()
    {
        var user = CreateAuthenticatedUser(
            new Claim(ClaimTypes.Email, "test@example.com"));
        var service = CreateService(user);

        var email = service.GetCurrentUserEmail();
        await Assert.That(email).IsEqualTo("test@example.com");
    }

    [Test]
    public async Task IsAuthenticated_AuthenticatedUser_ReturnsTrue()
    {
        var user = CreateAuthenticatedUser(
            new Claim(ClaimTypes.NameIdentifier, "user-1"));
        var service = CreateService(user);

        var isAuthenticated = service.IsAuthenticated();
        await Assert.That(isAuthenticated).IsTrue();
    }

    [Test]
    public async Task IsAuthenticated_NoUser_ReturnsFalse()
    {
        // DefaultHttpContext has an unauthenticated user by default
        var service = CreateService();

        var isAuthenticated = service.IsAuthenticated();
        await Assert.That(isAuthenticated).IsFalse();
    }

    [Test]
    public async Task IsAuthenticated_UnauthenticatedUser_ReturnsFalse()
    {
        var user = CreateUnauthenticatedUser();
        var service = CreateService(user);

        var isAuthenticated = service.IsAuthenticated();
        await Assert.That(isAuthenticated).IsFalse();
    }

    [Test]
    public async Task IsEmailVerified_TrueClaim_ReturnsTrue()
    {
        var user = CreateAuthenticatedUser(
            new Claim("EmailVerified", "True"));
        var service = CreateService(user);

        var isVerified = service.IsEmailVerified();
        await Assert.That(isVerified).IsTrue();
    }

    [Test]
    public async Task IsEmailVerified_FalseClaim_ReturnsFalse()
    {
        var user = CreateAuthenticatedUser(
            new Claim("EmailVerified", "False"));
        var service = CreateService(user);

        var isVerified = service.IsEmailVerified();
        await Assert.That(isVerified).IsFalse();
    }

    [Test]
    public async Task GetCurrentUserRole_WithClaim_ReturnsRole()
    {
        var user = CreateAuthenticatedUser(
            new Claim(ClaimTypes.Role, "GlobalAdmin"));
        var service = CreateService(user);

        var role = service.GetCurrentUserRole();
        await Assert.That(role).IsEqualTo("GlobalAdmin");
    }

    [Test]
    public async Task GetCurrentUserRole_NoClaim_ReturnsNull()
    {
        var user = CreateAuthenticatedUser();
        var service = CreateService(user);

        var role = service.GetCurrentUserRole();
        await Assert.That(role).IsNull();
    }
}
