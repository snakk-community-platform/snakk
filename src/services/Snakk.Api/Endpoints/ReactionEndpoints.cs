namespace Snakk.Api.Endpoints;

using Snakk.Api.Models;
using Snakk.Application.UseCases;
using Snakk.Domain.Extensions;
using Snakk.Domain.ValueObjects;
using System.Security.Claims;

public static class ReactionEndpoints
{
    public static void MapReactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/posts/{postId}/reactions")
            .WithTags("Reactions");

        group.MapPost("/", ToggleReactionAsync)
            .WithName("ToggleReaction")
            .RequireAuthorization();

        group.MapGet("/", GetReactionCountsAsync)
            .WithName("GetReactionCounts");

        group.MapGet("/me", GetMyReactionAsync)
            .WithName("GetMyReaction");
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
        if (userIdClaim == null)
            return Results.Unauthorized();

        var result = await reactionUseCase.ToggleReactionAsync(
            PostId.From(postId),
            UserId.From(userIdClaim.Value),
            request.Type.ToDomain());

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(new { added = result.Value });
    }

    private static async Task<IResult> GetReactionCountsAsync(
        string postId,
        ReactionUseCase reactionUseCase)
    {
        var counts = await reactionUseCase.GetReactionCountsAsync(PostId.From(postId));

        return Results.Ok(new
        {
            thumbsUp = counts.GetValueOrDefault(ReactionType.ThumbsUp, 0),
            heart = counts.GetValueOrDefault(ReactionType.Heart, 0),
            eyes = counts.GetValueOrDefault(ReactionType.Eyes, 0)
        });
    }

    private static async Task<IResult> GetMyReactionAsync(
        string postId,
        HttpContext httpContext,
        ReactionUseCase reactionUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Ok(new { reaction = (string?)null });

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Ok(new { reaction = (string?)null });

        var reaction = await reactionUseCase.GetUserReactionAsync(
            PostId.From(postId),
            UserId.From(userIdClaim.Value));

        return Results.Ok(new { reaction = reaction?.ToString() });
    }
}
