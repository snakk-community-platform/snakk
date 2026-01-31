namespace Snakk.Api.Endpoints;

using Microsoft.AspNetCore.Authentication;
using Snakk.Api.Helpers;
using Snakk.Api.Models;
using Snakk.Api.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using System.Security.Claims;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Authentication");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register");

        group.MapPost("/login", LoginAsync)
            .WithName("Login");

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout");

        group.MapPost("/refresh", RefreshTokenAsync)
            .WithName("RefreshToken")
            .RequireRateLimiting("auth");

        group.MapGet("/verify-email", VerifyEmailAsync)
            .WithName("VerifyEmail");

        group.MapGet("/status", GetAuthStatus)
            .WithName("GetAuthStatus");

        group.MapGet("/me", GetCurrentUserAsync)
            .WithName("GetCurrentUser");

        group.MapPut("/update-profile", UpdateProfileAsync)
            .WithName("UpdateProfile")
            .RequireAuthorization();

        group.MapPut("/preferences", UpdatePreferencesAsync)
            .WithName("UpdatePreferences")
            .RequireAuthorization();

        group.MapGet("/oauth/{provider}/challenge", OAuthChallengeAsync)
            .WithName("OAuthChallenge");

        group.MapGet("/oauth/callback", OAuthCallbackAsync)
            .WithName("OAuthCallback");
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        AuthenticationUseCase authUseCase,
        IJwtTokenService jwtService,
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

        // Generate JWT for immediate login
        var jwt = jwtService.GenerateToken(user);

        // Generate refresh token
        var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(user.PublicId);
        if (!refreshTokenResult.IsSuccess)
            return Results.Problem("Registration succeeded but failed to create refresh token");

        AuthAuditLogger.LogRegistration(logger, request.Email, ipAddress, userAgent);

        return Results.Ok(new
        {
            message = "Registration successful. Please check your email to verify your account.",
            accessToken = jwt,
            refreshToken = refreshTokenResult.Value!.Value,
            user = new
            {
                id = user.PublicId.Value,
                email = user.Email,
                displayName = user.DisplayName,
                emailVerified = user.EmailVerified
            }
        });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AuthenticationUseCase authUseCase,
        IJwtTokenService jwtService,
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

        // Generate JWT
        var jwt = jwtService.GenerateToken(user);

        // Generate refresh token
        var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(user.PublicId);
        if (!refreshTokenResult.IsSuccess)
            return Results.Problem("Failed to create refresh token");

        AuthAuditLogger.LogLoginSuccess(logger, request.Email, ipAddress, userAgent);

        return Results.Ok(new
        {
            accessToken = jwt,
            refreshToken = refreshTokenResult.Value!.Value,
            user = new
            {
                id = user.PublicId.Value,
                email = user.Email,
                displayName = user.DisplayName,
                emailVerified = user.EmailVerified
            }
        });
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        AuthenticationUseCase authUseCase,
        ILogger<object> logger)
    {
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId != null)
        {
            var ipAddress = AuthAuditLogger.GetClientIp(httpContext);
            var userAgent = AuthAuditLogger.GetUserAgent(httpContext);

            await authUseCase.RevokeRefreshTokensAsync(UserId.From(userId));

            AuthAuditLogger.LogLogout(logger, userId, ipAddress, userAgent);
        }

        await httpContext.SignOutAsync("Cookies");
        return Results.Ok(new { message = "Logged out successfully" });
    }

    private static async Task<IResult> RefreshTokenAsync(
        RefreshTokenRequest request,
        AuthenticationUseCase authUseCase,
        IJwtTokenService jwtService,
        HttpContext httpContext,
        ILogger<object> logger)
    {
        var result = await authUseCase.RefreshTokenAsync(request.RefreshToken);
        if (!result.IsSuccess)
            return Results.Unauthorized();

        var (user, newRefreshToken) = result.Value;
        var jwt = jwtService.GenerateToken(user);

        var ipAddress = AuthAuditLogger.GetClientIp(httpContext);
        var userAgent = AuthAuditLogger.GetUserAgent(httpContext);
        AuthAuditLogger.LogTokenRefresh(logger, user.PublicId.Value, ipAddress, userAgent);

        return Results.Ok(new
        {
            accessToken = jwt,
            refreshToken = newRefreshToken.Value
        });
    }

    private static async Task<IResult> VerifyEmailAsync(
        string token,
        AuthenticationUseCase authUseCase)
    {
        var result = await authUseCase.VerifyEmailAsync(token);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(new { message = "Email verified successfully. You can now log in." });
    }

    private static IResult GetAuthStatus(Snakk.Api.Services.ICurrentUserService currentUser)
    {
        if (!currentUser.IsAuthenticated())
        {
            return Results.Ok(new { isAuthenticated = false });
        }

        return Results.Ok(new
        {
            isAuthenticated = true,
            publicId = currentUser.GetCurrentUserId(),
            displayName = currentUser.GetCurrentUserDisplayName(),
            emailVerified = currentUser.IsEmailVerified()
        });
    }

    private static async Task<IResult> GetCurrentUserAsync(
        Snakk.Api.Services.ICurrentUserService currentUser,
        AuthenticationUseCase authUseCase)
    {
        if (!currentUser.IsAuthenticated())
            return Results.Unauthorized();

        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue == null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdValue);
        var result = await authUseCase.GetUserByIdAsync(userId);

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return Results.Ok(new
        {
            publicId = result.Value!.PublicId.Value,
            displayName = result.Value.DisplayName,
            email = result.Value.Email,
            emailVerified = result.Value.EmailVerified,
            oAuthProvider = result.Value.OAuthProvider,
            preferEndlessScroll = result.Value.PreferEndlessScroll
        });
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        Snakk.Api.Services.ICurrentUserService currentUser,
        AuthenticationUseCase authUseCase,
        IJwtTokenService jwtService)
    {
        if (!currentUser.IsAuthenticated())
            return Results.Unauthorized();

        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue == null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdValue);
        var result = await authUseCase.UpdateDisplayNameAsync(userId, request.DisplayName);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        // Generate new JWT token with updated display name
        var userResult = await authUseCase.GetUserByIdAsync(userId);
        if (userResult.IsSuccess)
        {
            var user = userResult.Value!;
            var newToken = jwtService.GenerateToken(
                user.PublicId.Value,
                user.DisplayName,
                user.Email,
                user.EmailVerified,
                user.OAuthProvider);

            return Results.Ok(new
            {
                message = "Profile updated successfully",
                token = newToken
            });
        }

        return Results.Ok(new { message = "Profile updated successfully" });
    }

    private static async Task<IResult> UpdatePreferencesAsync(
        UpdatePreferencesRequest request,
        Snakk.Api.Services.ICurrentUserService currentUser,
        AuthenticationUseCase authUseCase)
    {
        if (!currentUser.IsAuthenticated())
            return Results.Unauthorized();

        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue == null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdValue);
        var result = await authUseCase.UpdatePreferencesAsync(userId, request.PreferEndlessScroll);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(new { message = "Preferences updated successfully" });
    }

    private static async Task<IResult> OAuthChallengeAsync(
        string provider,
        string? returnUrl,
        HttpContext httpContext,
        IConfiguration configuration)
    {
        // Check if the provider is configured
        var schemes = await httpContext.RequestServices
            .GetRequiredService<IAuthenticationSchemeProvider>()
            .GetAllSchemesAsync();

        if (!schemes.Any(s => s.Name == provider))
        {
            var webClientUrl = configuration["WebClientUrl"] ?? "https://localhost:7001";
            return Results.Redirect($"{webClientUrl}/auth/login?error={Uri.EscapeDataString($"OAuth provider '{provider}' is not configured. Please add valid credentials to appsettings.json.")}");
        }

        // Generate CSRF state token for OAuth flow protection
        var stateToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        // Store state in secure HTTP-only cookie (short-lived, 10 minutes)
        httpContext.Response.Cookies.Append("oauth_state", stateToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMinutes(10)
        });

        var authProperties = new AuthenticationProperties
        {
            RedirectUri = $"/auth/oauth/callback?provider={provider}&returnUrl={returnUrl ?? "/"}&state={Uri.EscapeDataString(stateToken)}"
        };

        return Results.Challenge(authProperties, new[] { provider });
    }

    private static async Task<IResult> OAuthCallbackAsync(
        string provider,
        string? returnUrl,
        string? state,
        HttpContext httpContext,
        AuthenticationUseCase authUseCase,
        IJwtTokenService jwtService,
        IConfiguration configuration,
        ILogger<object> logger)
    {
        var ipAddress = AuthAuditLogger.GetClientIp(httpContext);
        var userAgent = AuthAuditLogger.GetUserAgent(httpContext);
        var webClientUrl = configuration["WebClientUrl"] ?? "https://localhost:7001";

        // Validate CSRF state token
        var storedState = httpContext.Request.Cookies["oauth_state"];
        if (string.IsNullOrEmpty(storedState) || storedState != state)
        {
            // Delete the cookie
            httpContext.Response.Cookies.Delete("oauth_state");
            AuthAuditLogger.LogOAuthStateValidationFailure(logger, provider, ipAddress, userAgent);
            return Results.Redirect($"{webClientUrl}/auth/login?error=invalid_state_token");
        }

        // Delete the state cookie after validation (single-use)
        httpContext.Response.Cookies.Delete("oauth_state");

        var authenticateResult = await httpContext.AuthenticateAsync("TempOAuth");

        if (!authenticateResult.Succeeded)
            return Results.Redirect($"{webClientUrl}/auth/login?error=oauth_failed");

        var email = authenticateResult.Principal.FindFirst(ClaimTypes.Email)?.Value;
        var nameIdentifier = authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var name = authenticateResult.Principal.FindFirst(ClaimTypes.Name)?.Value
                   ?? email?.Split('@')[0]
                   ?? "User";

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(nameIdentifier))
            return Results.Redirect($"{webClientUrl}/auth/login?error=invalid_oauth_response");

        var result = await authUseCase.LoginWithOAuthAsync(provider, nameIdentifier, email, name);

        if (!result.IsSuccess)
            return Results.Redirect($"{webClientUrl}/auth/login?error={Uri.EscapeDataString(result.Error ?? "Unknown error")}");

        var user = result.Value!;

        // Audit log OAuth login
        AuthAuditLogger.LogOAuthLogin(logger, provider, email, ipAddress, userAgent);

        // Generate JWT token
        var token = jwtService.GenerateToken(user);

        // Generate refresh token
        var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(user.PublicId);
        if (!refreshTokenResult.IsSuccess)
            return Results.Redirect($"{webClientUrl}/auth/login?error={Uri.EscapeDataString("Failed to create refresh token")}");

        // Check if user was just created (within last 30 seconds) - redirect to profile setup
        var isNewUser = (DateTime.UtcNow - user.CreatedAt).TotalSeconds < 30;

        if (isNewUser)
        {
            // New users go to profile setup page with tokens in URL fragment
            var setupFragment = $"#access_token={Uri.EscapeDataString(token)}&refresh_token={Uri.EscapeDataString(refreshTokenResult.Value!.Value)}";
            return Results.Redirect($"{webClientUrl}/auth/setup-profile{setupFragment}");
        }

        // Existing users go to OAuth completion page with tokens in URL fragment
        var finalReturnUrl = !string.IsNullOrEmpty(returnUrl) ? returnUrl : "/";
        var fragment = $"#access_token={Uri.EscapeDataString(token)}&refresh_token={Uri.EscapeDataString(refreshTokenResult.Value!.Value)}&returnUrl={Uri.EscapeDataString(finalReturnUrl)}";
        return Results.Redirect($"{webClientUrl}/auth/oauth-complete{fragment}");
    }
}
