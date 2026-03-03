namespace Snakk.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Snakk.Api.Helpers;
using Snakk.Api.Models;
using Snakk.Api.Services;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;
using Snakk.Shared.Helpers;
using Snakk.Application.DTOs.Responses;
using System.Security.Claims;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Authentication");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .Produces<RegisterResponse>()
            .RequireRateLimiting("auth");

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .Produces<Application.DTOs.Auth.LoginResponse>()
            .RequireRateLimiting("auth");

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .Produces<MessageResponse>();

        group.MapPost("/refresh", RefreshTokenAsync)
            .WithName("RefreshToken")
            .Produces<Application.DTOs.Auth.RefreshTokenResponse>()
            .RequireRateLimiting("auth");

        group.MapGet("/verify-email", VerifyEmailAsync)
            .WithName("VerifyEmail")
            .Produces<MessageResponse>();

        group.MapGet("/status", GetAuthStatus)
            .WithName("GetAuthStatus")
            .Produces<Application.DTOs.Auth.AuthStatusResponse>();
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        AuthenticationUseCase authUseCase,
        IJwtTokenService jwtService,
        SnakkDbContext context,
        HttpContext httpContext,
        ILogger<object> logger)
    {
        var ipAddress = AuthAuditLogger.GetClientIp(httpContext);
        var userAgent = AuthAuditLogger.GetUserAgent(httpContext);

        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var result = await authUseCase.RegisterWithEmailAsync(
            request.Email,
            request.Password,
            request.DisplayName,
            baseUrl);

        if (!result.IsSuccess)
        {
            // Generic error message to prevent account enumeration
            // Don't leak whether email already exists
            return Results.BadRequest(new { error = "Registration failed. Please check your details and try again." });
        }

        var user = result.Value!;

        // Fetch user roles from UserRoles table (new users typically have no roles)
        var userDbEntity = await context.Users
            .Include(u => u.Roles.Where(r => r.RevokedAt == null))
            .FirstOrDefaultAsync(u => u.PublicId == user.PublicId.Value);

        var roles = userDbEntity?.Roles
            .Select(r => ((UserRoleTypeEnum)r.RoleId).ToString())
            .ToList() ?? [];

        // Generate JWT for immediate login
        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            user.OAuthProvider,
            roles.FirstOrDefault());

        // Generate refresh token
        var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(user.PublicId);

        if (!refreshTokenResult.IsSuccess)
            return Results.Problem("Registration succeeded but failed to create refresh token");

        AuthAuditLogger.LogRegistration(logger, request.Email, ipAddress, userAgent);

        return TypedResults.Ok(new RegisterResponse(
            Message: "Registration successful. Please check your email to verify your account.",
            AccessToken: jwt,
            RefreshToken: refreshTokenResult.Value!.Value,
            User: new RegisterUserInfo(
                Id: user.PublicId.Value,
                Email: user.Email,
                DisplayName: user.DisplayName,
                EmailVerified: user.EmailVerified)));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AuthenticationUseCase authUseCase,
        IJwtTokenService jwtService,
        SnakkDbContext context,
        HttpContext httpContext,
        ILogger<object> logger)
    {
        var ipAddress = AuthAuditLogger.GetClientIp(httpContext);
        var userAgent = AuthAuditLogger.GetUserAgent(httpContext);

        var result = await authUseCase.LoginWithEmailAsync(request.Email, request.Password);

        if (!result.IsSuccess)
        {
            AuthAuditLogger.LogLoginFailure(logger, request.Email, ipAddress, userAgent);
            return Results.Unauthorized();
        }

        var user = result.Value!;

        // Fetch user roles from UserRoles table
        var userDbEntity = await context.Users
            .Include(u => u.Roles.Where(r => r.RevokedAt == null))
            .FirstOrDefaultAsync(u => u.PublicId == user.PublicId.Value);

        var roles = userDbEntity?.Roles
            .Select(r => ((UserRoleTypeEnum)r.RoleId).ToString())
            .ToList() ?? [];

        // Generate JWT with roles (using first role for backward compatibility with single-role JWT service)
        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            user.OAuthProvider,
            roles.FirstOrDefault());

        // Generate refresh token
        var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(user.PublicId);

        if (!refreshTokenResult.IsSuccess)
            return Results.Problem("Failed to create refresh token");

        AuthAuditLogger.LogLoginSuccess(logger, request.Email, ipAddress, userAgent);

        return TypedResults.Ok(new Application.DTOs.Auth.LoginResponse
        {
            AccessToken = jwt,
            RefreshToken = refreshTokenResult.Value!.Value,
            User = new Application.DTOs.Auth.UserInfo
            {
                Id = user.PublicId.Value,
                Email = user.Email,
                DisplayName = user.DisplayName,
                EmailVerified = user.EmailVerified,
                Roles = roles
            }
        });
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        AuthenticationUseCase authUseCase,
        ILogger<object> logger)
    {
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId is not null)
        {
            var ipAddress = AuthAuditLogger.GetClientIp(httpContext);
            var userAgent = AuthAuditLogger.GetUserAgent(httpContext);

            await authUseCase.RevokeRefreshTokensAsync(UserId.From(userId));

            AuthAuditLogger.LogLogout(logger, userId, ipAddress, userAgent);
        }

        return TypedResults.Ok(new MessageResponse("Logged out successfully"));
    }

    private static async Task<IResult> RefreshTokenAsync(
        RefreshTokenRequest request,
        AuthenticationUseCase authUseCase,
        IJwtTokenService jwtService,
        SnakkDbContext context,
        HttpContext httpContext,
        ILogger<object> logger)
    {
        var result = await authUseCase.RefreshTokenAsync(request.RefreshToken);

        if (!result.IsSuccess)
            return Results.Unauthorized();

        var (user, newRefreshToken) = result.Value;

        // Fetch user roles from UserRoles table
        var userDbEntity = await context.Users
            .Include(u => u.Roles.Where(r => r.RevokedAt == null))
            .FirstOrDefaultAsync(u => u.PublicId == user.PublicId.Value);

        var roles = userDbEntity?.Roles
            .Select(r => ((UserRoleTypeEnum)r.RoleId).ToString())
            .ToList() ?? [];

        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            user.OAuthProvider,
            roles.FirstOrDefault());

        var ipAddress = AuthAuditLogger.GetClientIp(httpContext);
        var userAgent = AuthAuditLogger.GetUserAgent(httpContext);
        AuthAuditLogger.LogTokenRefresh(logger, user.PublicId.Value, ipAddress, userAgent);

        return TypedResults.Ok(new Application.DTOs.Auth.RefreshTokenResponse
        {
            AccessToken = jwt,
            RefreshToken = newRefreshToken.Value
        });
    }

    private static async Task<IResult> VerifyEmailAsync(
        string token,
        AuthenticationUseCase authUseCase)
    {
        var result = await authUseCase.VerifyEmailAsync(token);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return TypedResults.Ok(new MessageResponse("Email verified successfully. You can now log in."));
    }

    private static IResult GetAuthStatus(
        Snakk.Api.Services.ICurrentUserService currentUser,
        HttpContext httpContext)
    {
        // Prevent browser caching of auth status (critical for logout to work correctly)
        httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        httpContext.Response.Headers.Pragma = "no-cache";
        httpContext.Response.Headers.Expires = "0";

        if (!currentUser.IsAuthenticated())
        {
            return TypedResults.Ok(new Application.DTOs.Auth.AuthStatusResponse
            {
                IsAuthenticated = false
            });
        }

        var userId = currentUser.GetCurrentUserId();

        return TypedResults.Ok(new Application.DTOs.Auth.AuthStatusResponse
        {
            IsAuthenticated = true,
            PublicId = userId,
            DisplayName = currentUser.GetCurrentUserDisplayName(),
            EmailVerified = currentUser.IsEmailVerified(),
            Role = currentUser.GetCurrentUserRole(),
            AvatarUrl = AvatarHelper.GetAvatarUrl(userId, AvatarEntityType.User, 0)
        });
    }
}
