namespace Snakk.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
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
            DisplayName: result.Value.DisplayName,
            Email: result.Value.Email ?? "",
            EmailVerified: result.Value.EmailVerified,
            OAuthProvider: result.Value.OAuthProvider,
            PreferEndlessScroll: result.Value.PreferEndlessScroll,
            AutoFollowOnReply: result.Value.AutoFollowOnReply,
            Timezone: result.Value.Timezone));
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        ICurrentUserService currentUser,
        AuthenticationUseCase authUseCase,
        IJwtTokenService jwtService,
        SnakkDbContext context)
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
                .FirstOrDefaultAsync(u => u.PublicId == user.PublicId.Value);

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
            request.PreferEndlessScroll,
            request.AutoFollowOnReply,
            request.Timezone);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return TypedResults.Ok(new MessageResponse("Preferences updated successfully"));
    }
}
