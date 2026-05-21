namespace Snakk.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Snakk.Api.Models;
using Snakk.Api.Services;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;
using Snakk.Application.DTOs.Responses;

public static class MeEndpoints
{
    public static void MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me")
            .WithTags("CurrentUser")
            .RequireAuthorization();

        group.MapGet("/", GetCurrentUserAsync)
            .WithName("GetCurrentUser")
            .Produces<CurrentUserResponse>();

        group.MapPut("/profile", UpdateProfileAsync)
            .WithName("UpdateProfile")
            .Produces<UpdateProfileResponse>();

        group.MapPut("/preferences", UpdatePreferencesAsync)
            .WithName("UpdatePreferences")
            .Produces<MessageResponse>();

        group.MapPost("/password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .Produces<MessageResponse>();

        group.MapPost("/verify-credential", VerifyCredentialAsync)
            .WithName("VerifyCredential")
            .Produces<MessageResponse>();

        group.MapPost("/sudo", IssueSudoTokenAsync)
            .WithName("IssueSudoToken")
            .RequireRateLimiting("auth");

        group.MapPost("/sudo/passkey/begin", BeginSudoPasskeyAsync)
            .WithName("BeginSudoPasskey")
            .RequireRateLimiting("auth");

        group.MapPost("/sudo/passkey/complete", CompleteSudoPasskeyAsync)
            .WithName("CompleteSudoPasskey")
            .RequireRateLimiting("auth");
    }

    internal static bool ValidateSudoToken(string? sudoToken, string userId, IMemoryCache cache)
    {
        if (string.IsNullOrWhiteSpace(sudoToken)) return false;
        return cache.TryGetValue($"sudo:{userId}:{sudoToken}", out _);
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ICurrentUserService currentUser,
        AuthenticationUseCase authUseCase)
    {
        var userIdValue = currentUser.GetCurrentUserId();

        if (userIdValue is null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdValue);
        var result = await authUseCase.GetUserByIdAsync(userId);

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return TypedResults.Ok(new CurrentUserResponse(
            PublicId: result.Value!.PublicId.Value,
            DisplayName: result.Value.DisplayName ?? "",
            Email: result.Value.Email ?? "",
            EmailVerified: result.Value.EmailVerified,
            OAuthProvider: result.Value.OAuthProvider,
            AutoFollowOnReply: result.Value.AutoFollowOnReply,
            Timezone: result.Value.Timezone,
            HasPassword: result.Value.HasPassword()));
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        ICurrentUserService currentUser,
        AuthenticationUseCase authUseCase,
        IJwtTokenService jwtService,
        SnakkDbContext context,
        CancellationToken ct)
    {
        var userIdValue = currentUser.GetCurrentUserId();

        if (userIdValue is null)
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

            var userDbEntity = await context.Users
                .Include(u => u.Roles.Where(r => r.RevokedAt == null))
                .FirstOrDefaultAsync(u => u.PublicId == user.PublicId.Value, ct);

            var roles = userDbEntity?.Roles
                .Select(r => ((UserRoleTypeEnum)r.RoleId).ToString())
                .ToList() ?? [];

            var newToken = jwtService.GenerateToken(
                user.PublicId.Value,
                user.DisplayName,
                user.Email,
                user.EmailVerified,
                user.OAuthProvider,
                roles.FirstOrDefault());

            return TypedResults.Ok(new UpdateProfileResponse("Profile updated successfully", newToken));
        }

        return TypedResults.Ok(new UpdateProfileResponse("Profile updated successfully"));
    }

    private static async Task<IResult> UpdatePreferencesAsync(
        UpdatePreferencesRequest request,
        ICurrentUserService currentUser,
        AuthenticationUseCase authUseCase)
    {
        var userIdValue = currentUser.GetCurrentUserId();

        if (userIdValue is null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdValue);
        var result = await authUseCase.UpdatePreferencesAsync(
            userId,
            request.AutoFollowOnReply,
            request.Timezone);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return TypedResults.Ok(new MessageResponse("Preferences updated successfully"));
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        ICurrentUserService currentUser,
        AuthenticationUseCase authUseCase,
        IMemoryCache cache,
        CancellationToken ct)
    {
        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null)
            return Results.Unauthorized();

        if (request.NewPassword != request.ConfirmPassword)
            return Results.BadRequest(new { error = "Passwords do not match." });

        if (!ValidateSudoToken(request.SudoToken, userIdValue, cache))
            return Results.Json(new { error = "Authentication required." }, statusCode: 403);

        var result = await authUseCase.ChangePasswordAsync(
            UserId.From(userIdValue),
            null, // identity already verified via sudo token
            request.NewPassword,
            ct);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return TypedResults.Ok(new MessageResponse("Password changed successfully."));
    }

    private static async Task<IResult> IssueSudoTokenAsync(
        [FromBody] SudoRequest request,
        ICurrentUserService currentUser,
        ITwoFactorAuthService twoFactorService,
        IPasswordHasher passwordHasher,
        SnakkDbContext context,
        IMemoryCache cache,
        CancellationToken ct)
    {
        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null)
            return Results.Unauthorized();

        var user = await context.Users
            .Where(u => u.PublicId == userIdValue)
            .Select(u => new { u.PasswordHash, u.TwoFactorEnabled })
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return Results.Unauthorized();

        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            if (string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { error = "Password is required." });

            if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
                return Results.BadRequest(new { error = "Incorrect password." });
        }

        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.TotpCode))
                return Results.BadRequest(new { error = "2FA code is required." });

            var (isValid, _) = await twoFactorService.VerifyTwoFactorCodeAsync(userIdValue, request.TotpCode, ct: ct);
            if (!isValid)
                return Results.BadRequest(new { error = "Invalid 2FA code." });
        }

        var token = System.Security.Cryptography.RandomNumberGenerator.GetHexString(64);
        cache.Set($"sudo:{userIdValue}:{token}", true, TimeSpan.FromMinutes(5));

        return Results.Ok(new { sudoToken = token, expiresInSeconds = 300 });
    }

    private static async Task<IResult> BeginSudoPasskeyAsync(
        ICurrentUserService currentUser,
        IPasskeyService passkeyService,
        SnakkDbContext context,
        CancellationToken ct)
    {
        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null) return Results.Unauthorized();

        var email = await context.Users
            .Where(u => u.PublicId == userIdValue)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

        var (optionsJson, challengeId) = await passkeyService.BeginLoginAsync(email, ct);
        return Results.Ok(new { optionsJson, challengeId });
    }

    private static async Task<IResult> CompleteSudoPasskeyAsync(
        [FromBody] SudoPasskeyCompleteRequest request,
        ICurrentUserService currentUser,
        IPasskeyService passkeyService,
        ITwoFactorAuthService twoFactorService,
        SnakkDbContext context,
        IMemoryCache cache,
        CancellationToken ct)
    {
        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null) return Results.Unauthorized();

        (int _, string PublicId) assertion;
        try
        {
            assertion = await passkeyService.CompleteLoginAsync(request.ChallengeId, request.AssertionResponseJson, ct);
        }
        catch
        {
            return Results.BadRequest(new { error = "Passkey verification failed." });
        }

        if (!string.Equals(assertion.PublicId, userIdValue, StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "Passkey does not belong to this account." });

        var twoFactorEnabled = await context.Users
            .Where(u => u.PublicId == userIdValue)
            .Select(u => u.TwoFactorEnabled)
            .FirstOrDefaultAsync(ct);

        if (twoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.TotpCode))
                return Results.BadRequest(new { error = "2FA code is required." });

            var (isValid, _) = await twoFactorService.VerifyTwoFactorCodeAsync(userIdValue, request.TotpCode, ct: ct);
            if (!isValid)
                return Results.BadRequest(new { error = "Invalid 2FA code." });
        }

        var token = System.Security.Cryptography.RandomNumberGenerator.GetHexString(64);
        cache.Set($"sudo:{userIdValue}:{token}", true, TimeSpan.FromMinutes(5));
        return Results.Ok(new { sudoToken = token, expiresInSeconds = 300 });
    }

    private static async Task<IResult> VerifyCredentialAsync(
        VerifyCredentialRequest request,
        ICurrentUserService currentUser,
        ITwoFactorAuthService twoFactorService,
        IPasswordHasher passwordHasher,
        SnakkDbContext context,
        IMemoryCache cache,
        CancellationToken ct)
    {
        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null)
            return Results.Unauthorized();

        if (!string.IsNullOrWhiteSpace(request.SudoToken))
        {
            if (!ValidateSudoToken(request.SudoToken, userIdValue, cache))
                return Results.Json(new { error = "Authentication required." }, statusCode: 403);
            return TypedResults.Ok(new MessageResponse("Verified."));
        }

        if (!string.IsNullOrWhiteSpace(request.TotpCode))
        {
            var (isValid, _) = await twoFactorService.VerifyTwoFactorCodeAsync(userIdValue, request.TotpCode, ct: ct);
            if (!isValid)
                return Results.BadRequest(new { error = "Invalid 2FA code." });
            return TypedResults.Ok(new MessageResponse("Verified."));
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var hash = await context.Users
                .Where(u => u.PublicId == userIdValue)
                .Select(u => u.PasswordHash)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrEmpty(hash))
                return Results.BadRequest(new { error = "No password set on this account." });

            if (!passwordHasher.VerifyPassword(request.Password, hash))
                return Results.BadRequest(new { error = "Incorrect password." });

            return TypedResults.Ok(new MessageResponse("Verified."));
        }

        return Results.BadRequest(new { error = "Authentication required." });
    }
}
