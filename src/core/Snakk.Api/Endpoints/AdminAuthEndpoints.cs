namespace Snakk.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database.Repositories;

public static class AdminAuthEndpoints
{
    public static void MapAdminAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/auth")
            .WithTags("Admin Authentication");

        group.MapPost("/login", LoginAsync)
            .WithName("AdminLogin")
            .AllowAnonymous();

        group.MapPost("/logout", LogoutAsync)
            .WithName("AdminLogout");

        group.MapGet("/me", GetCurrentAdminAsync)
            .WithName("GetCurrentAdmin")
            .RequireAuthorization();
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] AdminLoginRequest request,
        IAdminAuthService adminAuthService,
        HttpContext httpContext,
        ILogger<object> logger)
    {
        var token = await adminAuthService.AuthenticateAsync(request.Email, request.Password);

        if (token == null)
        {
            logger.LogWarning("Failed admin login attempt for email: {Email}", request.Email);
            return Results.Unauthorized();
        }

        // Set HTTP-only cookie with the JWT token
        httpContext.Response.Cookies.Append("admin_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // Set to true in production with HTTPS
            SameSite = SameSiteMode.Lax, // Lax allows cookies on cross-origin GET requests
            MaxAge = TimeSpan.FromHours(8),
            Path = "/"
        });

        logger.LogInformation("Successful admin login for email: {Email}", request.Email);

        return Results.Ok(new
        {
            message = "Login successful"
        });
    }

    private static IResult LogoutAsync(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete("admin_token");
        return Results.Ok(new { message = "Logout successful" });
    }

    private static async Task<IResult> GetCurrentAdminAsync(
        HttpContext httpContext,
        IAdminUserRepository adminUserRepository)
    {
        // Get admin user ID from claims (set by authentication middleware)
        var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Unauthorized();

        var adminUser = await adminUserRepository.GetByPublicIdAsync(userIdClaim.Value);
        if (adminUser == null || !adminUser.IsActive)
            return Results.Unauthorized();

        return Results.Ok(new
        {
            id = adminUser.PublicId,
            email = adminUser.Email,
            displayName = adminUser.DisplayName,
            isActive = adminUser.IsActive,
            lastLoginAt = adminUser.LastLoginAt
        });
    }
}

public record AdminLoginRequest(string Email, string Password);
