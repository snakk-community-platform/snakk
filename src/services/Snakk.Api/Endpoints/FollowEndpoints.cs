namespace Snakk.Api.Endpoints;

using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using System.Security.Claims;

public static class FollowEndpoints
{
    public static void MapFollowEndpoints(this IEndpointRouteBuilder app)
    {
        // Discussion follow endpoints
        var discussionGroup = app.MapGroup("/api/discussions/{discussionId}")
            .WithTags("Follow");

        discussionGroup.MapPost("/follow", ToggleFollowDiscussionAsync)
            .WithName("ToggleFollowDiscussion")
            .RequireAuthorization();

        discussionGroup.MapGet("/follow-status", GetDiscussionFollowStatusAsync)
            .WithName("GetDiscussionFollowStatus");

        // Space follow endpoints
        var spaceGroup = app.MapGroup("/api/spaces/{spaceId}")
            .WithTags("Follow");

        spaceGroup.MapPost("/follow", ToggleFollowSpaceAsync)
            .WithName("ToggleFollowSpace")
            .RequireAuthorization();

        spaceGroup.MapPut("/follow-level", UpdateSpaceFollowLevelAsync)
            .WithName("UpdateSpaceFollowLevel")
            .RequireAuthorization();

        spaceGroup.MapGet("/follow-status", GetSpaceFollowStatusAsync)
            .WithName("GetSpaceFollowStatus");

        // User follow endpoints
        var userGroup = app.MapGroup("/api/users/{userId}")
            .WithTags("Follow");

        userGroup.MapPost("/follow", ToggleFollowUserAsync)
            .WithName("ToggleFollowUser")
            .RequireAuthorization();

        userGroup.MapGet("/follow-status", GetUserFollowStatusAsync)
            .WithName("GetUserFollowStatus");

        // Follow list endpoints (for caching)
        var followGroup = app.MapGroup("/api/follows")
            .WithTags("Follow")
            .RequireAuthorization();

        followGroup.MapGet("/spaces", GetFollowedSpacesAsync)
            .WithName("GetFollowedSpaces");

        followGroup.MapGet("/discussions", GetFollowedDiscussionsAsync)
            .WithName("GetFollowedDiscussions");

        followGroup.MapGet("/users", GetFollowedUsersAsync)
            .WithName("GetFollowedUsers");
    }

    private static async Task<IResult> ToggleFollowDiscussionAsync(
        string discussionId,
        HttpContext httpContext,
        FollowUseCase followUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Unauthorized();

        var result = await followUseCase.ToggleFollowDiscussionAsync(
            UserId.From(userIdClaim.Value),
            DiscussionId.From(discussionId));

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(new { isFollowing = result.Value });
    }

    private static async Task<IResult> GetDiscussionFollowStatusAsync(
        string discussionId,
        HttpContext httpContext,
        FollowUseCase followUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Ok(new { isFollowing = false });

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Ok(new { isFollowing = false });

        var isFollowing = await followUseCase.IsFollowingDiscussionAsync(
            UserId.From(userIdClaim.Value),
            DiscussionId.From(discussionId));

        return Results.Ok(new { isFollowing });
    }

    private static async Task<IResult> ToggleFollowSpaceAsync(
        string spaceId,
        string? level,
        HttpContext httpContext,
        FollowUseCase followUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Unauthorized();

        var followLevel = level?.ToLowerInvariant() switch
        {
            "posts" or "discussionsandposts" => FollowLevel.DiscussionsAndPosts,
            _ => FollowLevel.DiscussionsOnly
        };

        var result = await followUseCase.ToggleFollowSpaceAsync(
            UserId.From(userIdClaim.Value),
            SpaceId.From(spaceId),
            followLevel);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(new { isFollowing = result.Value, level = followLevel.ToString() });
    }

    private static async Task<IResult> UpdateSpaceFollowLevelAsync(
        string spaceId,
        string level,
        HttpContext httpContext,
        FollowUseCase followUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Unauthorized();

        var followLevel = level?.ToLowerInvariant() switch
        {
            "posts" or "discussionsandposts" => FollowLevel.DiscussionsAndPosts,
            _ => FollowLevel.DiscussionsOnly
        };

        var result = await followUseCase.UpdateSpaceFollowLevelAsync(
            UserId.From(userIdClaim.Value),
            SpaceId.From(spaceId),
            followLevel);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(new { level = result.Value.ToString() });
    }

    private static async Task<IResult> GetSpaceFollowStatusAsync(
        string spaceId,
        HttpContext httpContext,
        FollowUseCase followUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Ok(new { isFollowing = false, level = (string?)null });

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Ok(new { isFollowing = false, level = (string?)null });

        var (isFollowing, level) = await followUseCase.GetSpaceFollowStatusAsync(
            UserId.From(userIdClaim.Value),
            SpaceId.From(spaceId));

        return Results.Ok(new { isFollowing, level = level?.ToString() });
    }

    private static async Task<IResult> ToggleFollowUserAsync(
        string userId,
        HttpContext httpContext,
        FollowUseCase followUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var currentUserIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (currentUserIdClaim == null)
            return Results.Unauthorized();

        var result = await followUseCase.ToggleFollowUserAsync(
            UserId.From(currentUserIdClaim.Value),
            UserId.From(userId));

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(new { isFollowing = result.Value });
    }

    private static async Task<IResult> GetUserFollowStatusAsync(
        string userId,
        HttpContext httpContext,
        FollowUseCase followUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Ok(new { isFollowing = false });

        var currentUserIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (currentUserIdClaim == null)
            return Results.Ok(new { isFollowing = false });

        var isFollowing = await followUseCase.IsFollowingUserAsync(
            UserId.From(currentUserIdClaim.Value),
            UserId.From(userId));

        return Results.Ok(new { isFollowing });
    }

    private static async Task<IResult> GetFollowedSpacesAsync(
        HttpContext httpContext,
        FollowUseCase followUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Unauthorized();

        var followedSpaces = await followUseCase.GetFollowedSpacesAsync(
            UserId.From(userIdClaim.Value));

        var publicIds = followedSpaces.Select(s => s.Value).ToList();
        return Results.Ok(new { publicIds });
    }

    private static async Task<IResult> GetFollowedDiscussionsAsync(
        HttpContext httpContext,
        FollowUseCase followUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Unauthorized();

        var followedDiscussions = await followUseCase.GetFollowedDiscussionsAsync(
            UserId.From(userIdClaim.Value));

        var publicIds = followedDiscussions.Select(d => d.Value).ToList();
        return Results.Ok(new { publicIds });
    }

    private static async Task<IResult> GetFollowedUsersAsync(
        HttpContext httpContext,
        FollowUseCase followUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Unauthorized();

        var followedUsers = await followUseCase.GetFollowedUsersAsync(
            UserId.From(userIdClaim.Value));

        var publicIds = followedUsers.Select(u => u.Value).ToList();
        return Results.Ok(new { publicIds });
    }
}
