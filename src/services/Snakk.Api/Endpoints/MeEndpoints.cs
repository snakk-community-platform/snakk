namespace Snakk.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Snakk.Api.Models;
using Snakk.Api.Services;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
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

        group.MapPost("/sudo/oauth", IssueSudoTokenFromOAuthNonceAsync)
            .WithName("IssueSudoTokenFromOAuth")
            .RequireRateLimiting("auth");

        group.MapDelete("/account", DeleteAccountAsync)
            .WithName("DeleteAccount")
            .RequireRateLimiting("auth");

        group.MapGet("/data-export", ExportMyDataAsync)
            .WithName("ExportMyData")
            .RequireRateLimiting("expensive");
    }

    internal static async Task<bool> ValidateSudoTokenAsync(string? sudoToken, string userId, IDistributedCache cache, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sudoToken)) return false;
        return await cache.GetStringAsync($"sudo:{userId}:{sudoToken}", ct) is not null;
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

        var user = result.Value!;
        var connectionsResult = await authUseCase.GetOAuthConnectionsAsync(user.PublicId.Value);
        var providers = connectionsResult.IsSuccess
            ? connectionsResult.Value!.Select(c => c.Provider).ToList()
            : new List<string>();

        return TypedResults.Ok(new CurrentUserResponse(
            PublicId: user.PublicId.Value,
            DisplayName: user.DisplayName ?? "",
            Email: user.Email ?? "",
            EmailVerified: user.EmailVerified,
            ConnectedProviders: providers,
            AutoFollowOnReply: user.AutoFollowOnReply,
            Timezone: user.Timezone,
            HasPassword: user.HasPassword()));
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        ICurrentUserService currentUser,
        AuthenticationUseCase authUseCase,
        IJwtTokenService jwtService,
        IMeDataService meDataService,
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

            var roles = await meDataService.GetUserRolesAsync(user.PublicId.Value, ct);

            var newToken = jwtService.GenerateToken(
                user.PublicId.Value,
                user.DisplayName,
                user.Email,
                user.EmailVerified,
                roles.FirstOrDefault(),
                twoFactorEnabled: user.TwoFactorEnabled,
                slug: user.Slug);

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
        IDistributedCache cache,
        CancellationToken ct)
    {
        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null)
            return Results.Unauthorized();

        if (request.NewPassword != request.ConfirmPassword)
            return Results.BadRequest(new { error = "Passwords do not match." });

        if (!await ValidateSudoTokenAsync(request.SudoToken, userIdValue, cache, ct))
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
        IMeDataService meDataService,
        IDistributedCache cache,
        CancellationToken ct)
    {
        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null)
            return Results.Unauthorized();

        var userCreds = await meDataService.GetUserCredentialDataAsync(userIdValue, ct);

        if (userCreds is null)
            return Results.Unauthorized();

        if (!string.IsNullOrEmpty(userCreds.PasswordHash))
        {
            if (string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { error = "Password is required." });

            if (!passwordHasher.VerifyPassword(request.Password, userCreds.PasswordHash))
                return Results.BadRequest(new { error = "Incorrect password." });
        }

        if (userCreds.TwoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.TotpCode))
                return Results.BadRequest(new { error = "2FA code is required." });

            var (isValid, _) = await twoFactorService.VerifyTwoFactorCodeAsync(userIdValue, request.TotpCode, ct: ct);
            if (!isValid)
                return Results.BadRequest(new { error = "Invalid 2FA code." });
        }

        var token = System.Security.Cryptography.RandomNumberGenerator.GetHexString(64);
        await cache.SetStringAsync($"sudo:{userIdValue}:{token}", "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }, ct);

        return Results.Ok(new { sudoToken = token, expiresInSeconds = 300 });
    }

    private static async Task<IResult> BeginSudoPasskeyAsync(
        ICurrentUserService currentUser,
        IPasskeyService passkeyService,
        IMeDataService meDataService,
        CancellationToken ct)
    {
        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null) return Results.Unauthorized();

        var email = await meDataService.GetEmailAsync(userIdValue, ct);

        var (optionsJson, challengeId) = await passkeyService.BeginLoginAsync(email, ct);
        return Results.Ok(new { optionsJson, challengeId });
    }

    private static async Task<IResult> CompleteSudoPasskeyAsync(
        [FromBody] SudoPasskeyCompleteRequest request,
        ICurrentUserService currentUser,
        IPasskeyService passkeyService,
        IDistributedCache cache,
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

        var token = System.Security.Cryptography.RandomNumberGenerator.GetHexString(64);
        await cache.SetStringAsync($"sudo:{userIdValue}:{token}", "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }, ct);
        return Results.Ok(new { sudoToken = token, expiresInSeconds = 300 });
    }

    private static async Task<IResult> IssueSudoTokenFromOAuthNonceAsync(
        [FromBody] OAuthSudoNonceRequest request,
        ICurrentUserService currentUser,
        IDistributedCache cache,
        CancellationToken ct)
    {
        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null)
            return Results.Unauthorized();

        var cacheKey = $"sudo-oauth-nonce:{userIdValue}:{request.Nonce}";
        if (await cache.GetStringAsync(cacheKey, ct) is null)
            return Results.BadRequest(new { error = "Invalid or expired nonce." });

        await cache.RemoveAsync(cacheKey, ct);

        var token = System.Security.Cryptography.RandomNumberGenerator.GetHexString(64);
        await cache.SetStringAsync($"sudo:{userIdValue}:{token}", "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }, ct);
        return Results.Ok(new { sudoToken = token, expiresInSeconds = 300 });
    }

    private static async Task<IResult> DeleteAccountAsync(
        [FromBody] DeleteAccountRequest request,
        ICurrentUserService currentUser,
        GdprUseCase gdprUseCase,
        IMeDataService meDataService,
        IDistributedCache cache,
        IEmailProtector emailProtector,
        CancellationToken ct)
    {
        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null) return Results.Unauthorized();

        if (!await ValidateSudoTokenAsync(request.SudoToken, userIdValue, cache, ct))
            return Results.Json(new { error = "Authentication required." }, statusCode: 403);

        var encryptedEmail = await meDataService.GetEncryptedEmailAsync(userIdValue, ct);

        if (string.IsNullOrEmpty(encryptedEmail))
            return Results.BadRequest(new { error = "No email address on this account." });

        var actualEmail = emailProtector.Unprotect(encryptedEmail);
        if (!string.Equals(request.Confirmation.Trim(), actualEmail, StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "Email address does not match." });

        var result = await gdprUseCase.DeleteAccountAsync(userIdValue, ct);
        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(new { message = "Account deleted successfully." });
    }

    private static async Task<IResult> ExportMyDataAsync(
        ICurrentUserService currentUser,
        GdprUseCase gdprUseCase,
        CancellationToken ct)
    {
        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null) return Results.Unauthorized();

        var bundle = await gdprUseCase.ExportUserDataAsync(userIdValue, ct);
        if (bundle is null) return Results.NotFound(new { error = "User not found." });

        var json = System.Text.Json.JsonSerializer.Serialize(bundle,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var fileName = $"snakk-data-export-{DateTime.UtcNow:yyyy-MM-dd}.json";
        return Results.File(bytes, "application/json", fileName);
    }

    private static async Task<IResult> VerifyCredentialAsync(
        VerifyCredentialRequest request,
        ICurrentUserService currentUser,
        ITwoFactorAuthService twoFactorService,
        IPasswordHasher passwordHasher,
        IMeDataService meDataService,
        IDistributedCache cache,
        CancellationToken ct)
    {
        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null)
            return Results.Unauthorized();

        if (!string.IsNullOrWhiteSpace(request.SudoToken))
        {
            if (!await ValidateSudoTokenAsync(request.SudoToken, userIdValue, cache, ct))
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
            var hash = await meDataService.GetPasswordHashAsync(userIdValue, ct);

            if (string.IsNullOrEmpty(hash))
                return Results.BadRequest(new { error = "No password set on this account." });

            if (!passwordHasher.VerifyPassword(request.Password, hash))
                return Results.BadRequest(new { error = "Incorrect password." });

            return TypedResults.Ok(new MessageResponse("Verified."));
        }

        return Results.BadRequest(new { error = "Authentication required." });
    }
}
