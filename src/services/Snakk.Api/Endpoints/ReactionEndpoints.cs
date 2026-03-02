namespace Snakk.Api.Endpoints;

using Snakk.Api.Models;
using Snakk.Application.DTOs.Responses;
using Snakk.Application.UseCases;
using Snakk.Domain.Extensions;
using Snakk.Domain.ValueObjects;
using System.Security.Claims;

public static class ReactionEndpoints
{
    public static void MapReactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/posts/{postId}/reactions")
            .WithTags("Reactions");

        group.MapPost("/", ToggleReactionAsync)
            .WithName("ToggleReaction")
            .Produces<ToggleReactionResponse>()
            .RequireAuthorization();

        group.MapGet("/", GetReactionCountsAsync)
            .WithName("GetReactionCounts")
            .Produces<GetReactionCountsResponse>();

        group.MapGet("/me", GetMyReactionAsync)
            .WithName("GetMyReaction")
            .Produces<UserReactionResponse>();
    }

    private static async Task<IResult> ToggleReactionAsync(
        string postId,
        ToggleReactionRequest request,
        HttpContext httpContext,
        ReactionUseCase reactionUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        var result = await reactionUseCase.ToggleReactionAsync(
            PostId.From(postId),
            UserId.From(userIdClaim.Value),
            request.Type.ToDomain());

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return TypedResults.Ok(new ToggleReactionResponse(result.Value));
    }

    private static async Task<IResult> GetReactionCountsAsync(
        string postId,
        ReactionUseCase reactionUseCase)
    {
        var counts = await reactionUseCase.GetReactionCountsAsync(PostId.From(postId));

        return TypedResults.Ok(new GetReactionCountsResponse(
            counts.GetValueOrDefault(ReactionType.ThumbsUp, 0),
            counts.GetValueOrDefault(ReactionType.Heart, 0),
            counts.GetValueOrDefault(ReactionType.Eyes, 0),
            counts.GetValueOrDefault(ReactionType.Crazy, 0)));
    }

    private static async Task<IResult> GetMyReactionAsync(
        string postId,
        HttpContext httpContext,
        ReactionUseCase reactionUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return TypedResults.Ok(new UserReactionResponse(null));

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return TypedResults.Ok(new UserReactionResponse(null));

        var reaction = await reactionUseCase.GetUserReactionAsync(
            PostId.From(postId),
            UserId.From(userIdClaim.Value));

        return TypedResults.Ok(new UserReactionResponse(reaction?.ToString()));
    }
}
