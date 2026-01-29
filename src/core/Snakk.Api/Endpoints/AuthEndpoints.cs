namespace Snakk.Api.Endpoints;

using Microsoft.AspNetCore.Authentication;
using Snakk.Api.Models;
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
        HttpContext httpContext)
    {
        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var result = await authUseCase.RegisterWithEmailAsync(
            request.Email,
            request.Password,
            request.DisplayName,
            baseUrl);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(new
        {
            message = "Registration successful. Please check your email to verify your account.",
            userId = result.Value!.PublicId.Value
        });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AuthenticationUseCase authUseCase,
        HttpContext httpContext)
    {
        var result = await authUseCase.LoginWithEmailAsync(request.Email, request.Password);

        if (!result.IsSuccess)
            return Results.Unauthorized();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Value!.PublicId.Value),
            new(ClaimTypes.Name, result.Value.DisplayName),
            new(ClaimTypes.Email, result.Value.Email ?? ""),
            new("EmailVerified", result.Value.EmailVerified.ToString())
        };

        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync("Cookies", principal);

        return Results.Ok(new
        {
            userId = result.Value.PublicId.Value,
            displayName = result.Value.DisplayName,
            email = result.Value.Email,
            emailVerified = result.Value.EmailVerified
        });
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync("Cookies");
        return Results.Ok(new { message = "Logged out successfully" });
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

    private static IResult GetAuthStatus(HttpContext httpContext)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
        {
            return Results.Ok(new { isAuthenticated = false });
        }

        var publicId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var displayName = httpContext.User.FindFirst(ClaimTypes.Name)?.Value ?? "User";
        var emailVerified = httpContext.User.FindFirst("EmailVerified")?.Value == "True";

        return Results.Ok(new
        {
            isAuthenticated = true,
            publicId,
            displayName,
            emailVerified
        });
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext httpContext,
        AuthenticationUseCase authUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdClaim.Value);
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
        HttpContext httpContext,
        AuthenticationUseCase authUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdClaim.Value);
        var result = await authUseCase.UpdateDisplayNameAsync(userId, request.DisplayName);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(new { message = "Profile updated successfully" });
    }

    private static async Task<IResult> UpdatePreferencesAsync(
        UpdatePreferencesRequest request,
        HttpContext httpContext,
        AuthenticationUseCase authUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdClaim.Value);
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

        var authProperties = new AuthenticationProperties
        {
            RedirectUri = $"/auth/oauth/callback?provider={provider}&returnUrl={returnUrl ?? "/"}"
        };

        return Results.Challenge(authProperties, new[] { provider });
    }

    private static async Task<IResult> OAuthCallbackAsync(
        string provider,
        string? returnUrl,
        HttpContext httpContext,
        AuthenticationUseCase authUseCase,
        IConfiguration configuration)
    {
        var webClientUrl = configuration["WebClientUrl"] ?? "https://localhost:7001";

        var authenticateResult = await httpContext.AuthenticateAsync("Cookies");

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

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Value!.PublicId.Value),
            new(ClaimTypes.Name, result.Value.DisplayName),
            new(ClaimTypes.Email, result.Value.Email ?? ""),
            new("EmailVerified", result.Value.EmailVerified.ToString()),
            new("OAuthProvider", result.Value.OAuthProvider ?? "")
        };

        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync("Cookies", principal);

        // Check if user was just created (within last 30 seconds) - redirect to profile setup
        var isNewUser = (DateTime.UtcNow - result.Value.CreatedAt).TotalSeconds < 30;

        if (isNewUser)
        {
            return Results.Redirect($"{webClientUrl}/auth/setup-profile");
        }

        var finalReturnUrl = !string.IsNullOrEmpty(returnUrl) ? returnUrl : webClientUrl;
        return Results.Redirect(finalReturnUrl);
    }
}
