namespace Snakk.Api.Endpoints;

using Snakk.Api.Helpers;
using Snakk.Api.Models;
using Snakk.Api.Services;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
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
            .Produces<Application.DTOs.Auth.LoginTwoFactorRequiredResponse>()
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
        ITurnstileService turnstileService,
        IAuthDataService authDataService,
        HttpContext httpContext,
        ILogger<object> logger,
        CancellationToken ct)
    {
        if (!await turnstileService.VerifyAsync(request.TurnstileToken ?? ""))
            return Results.BadRequest(new { error = "Captcha verification failed. Please try again." });

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
        var roles = await authDataService.GetUserRolesAsync(user.PublicId.Value, ct);

        // Generate JWT for immediate login
        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            roles.FirstOrDefault(),
            twoFactorEnabled: user.TwoFactorEnabled,
            slug: user.Slug);

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
                Email: user.Email ?? "",
                DisplayName: user.DisplayName ?? "",
                EmailVerified: user.EmailVerified)));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AuthenticationUseCase authUseCase,
        IJwtTokenService jwtService,
        ITurnstileService turnstileService,
        IAuthDataService authDataService,
        HttpContext httpContext,
        ILogger<object> logger,
        IUserGrantsCacheService grantsCache,
        CancellationToken ct)
    {
        if (!await turnstileService.VerifyAsync(request.TurnstileToken ?? ""))
            return Results.BadRequest(new { error = "Captcha verification failed. Please try again." });

        var ipAddress = AuthAuditLogger.GetClientIp(httpContext);
        var userAgent = AuthAuditLogger.GetUserAgent(httpContext);

        var result = await authUseCase.LoginWithEmailAsync(request.Email, request.Password);

        if (!result.IsSuccess)
        {
            AuthAuditLogger.LogLoginFailure(logger, request.Email, ipAddress, userAgent);
            return Results.Unauthorized();
        }

        var user = result.Value!;

        // Short-circuit 2FA-enabled accounts so REST matches the gRPC Login contract
        // (which returns TwoFactorRequired=true instead of issuing tokens). Without this
        // gate the password was the only required factor on the REST surface.
        var twoFactorEnabled = await authDataService.GetTwoFactorEnabledAsync(user.PublicId.Value, ct);

        if (twoFactorEnabled)
        {
            var pendingToken = jwtService.GenerateTwoFactorPendingToken(user.PublicId.Value);
            AuthAuditLogger.LogLoginSuccess(logger, request.Email, ipAddress, userAgent);
            return TypedResults.Ok(new Application.DTOs.Auth.LoginTwoFactorRequiredResponse
            {
                TwoFactorPendingToken = pendingToken,
                User = new Application.DTOs.Auth.TwoFactorPendingUserInfo(
                    user.Email ?? "",
                    user.DisplayName)
            });
        }

        // Fetch user roles from UserRoles table
        var roles = await authDataService.GetUserRolesAsync(user.PublicId.Value, ct);

        // Generate JWT with roles (using first role for backward compatibility with single-role JWT service)
        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            roles.FirstOrDefault(),
            twoFactorEnabled: user.TwoFactorEnabled,
            slug: user.Slug);

        // Generate refresh token
        var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(user.PublicId);

        if (!refreshTokenResult.IsSuccess)
            return Results.Problem("Failed to create refresh token");

        AuthAuditLogger.LogLoginSuccess(logger, request.Email, ipAddress, userAgent);

        // Warm the grant cache so the user's first post-login request doesn't pay the DB cost.
        await grantsCache.GetGrantsAsync(user.PublicId.Value);

        return TypedResults.Ok(new Application.DTOs.Auth.LoginResponse
        {
            AccessToken = jwt,
            RefreshToken = refreshTokenResult.Value!.Value,
            User = new Application.DTOs.Auth.UserInfo
            {
                Id = user.PublicId.Value,
                Email = user.Email ?? "",
                DisplayName = user.DisplayName ?? "",
                EmailVerified = user.EmailVerified,
                Roles = roles
            }
        });
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        AuthenticationUseCase authUseCase,
        IJwtTokenService jwtService,
        ILogger<object> logger)
    {
        var authHeader = httpContext.Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            jwtService.RevokeToken(authHeader["Bearer ".Length..].Trim());

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
        IAuthDataService authDataService,
        HttpContext httpContext,
        ILogger<object> logger,
        CancellationToken ct)
    {
        var result = await authUseCase.RefreshTokenAsync(request.RefreshToken);

        if (!result.IsSuccess)
            return Results.Unauthorized();

        var (user, newRefreshToken) = result.Value;

        // Fetch user roles from UserRoles table
        var roles = await authDataService.GetUserRolesAsync(user.PublicId.Value, ct);

        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            roles.FirstOrDefault(),
            twoFactorEnabled: user.TwoFactorEnabled,
            slug: user.Slug);

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
            AvatarUrl = AvatarHelper.GetAvatarUrl(userId ?? "", AvatarEntityType.User, 0)
        });
    }
}
