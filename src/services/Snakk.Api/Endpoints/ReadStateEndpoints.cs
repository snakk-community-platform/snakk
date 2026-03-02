namespace Snakk.Api.Endpoints;

using Snakk.Application.DTOs.Responses;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using System.Security.Claims;
using Snakk.Api.Extensions;

public static class ReadStateEndpoints
{
    public static void MapReadStateEndpoints(this IEndpointRouteBuilder app)
    {
        // All read state endpoints require authentication
        var group = app.MapGroup("/discussions/{discussionId}")
            .WithTags("Read State")
            .RequireAuthorization();

        group.MapGet("/read-state", GetReadStateAsync)
            .WithName("GetReadState")
            .Produces<ReadStateResponse>();

        group.MapPost("/mark-read", MarkAsReadAsync)
            .WithName("MarkAsRead")
            .Produces<SuccessResponse>();

        // Batch endpoint
        var batchGroup = app.MapGroup("/read-states")
            .WithTags("Read State")
            .RequireAuthorization();

        batchGroup.MapPost("/batch", BatchMarkAsReadAsync)
            .WithName("BatchMarkAsRead")
            .Produces<SuccessResponse>();
    }

    private static async Task<IResult> GetReadStateAsync(
        string discussionId,
        HttpContext context,
        IDiscussionReadStateRepository readStateRepository)
    {
        // SECURITY: Extract userId from JWT, NOT query parameter
        var userId = context.User.GetUserId();

        if (userId is null)
            return Results.Unauthorized();

        var readState = await readStateRepository.GetAsync(
            userId,
            DiscussionId.From(discussionId));

        if (readState is null)
            return TypedResults.Ok(new ReadStateResponse(null, null));

        return TypedResults.Ok(new ReadStateResponse(readState.LastReadPostId?.Value, readState.LastReadAt));
    }

    private static async Task<IResult> MarkAsReadAsync(
        string discussionId,
        string postId,
        HttpContext context,
        IDiscussionReadStateRepository readStateRepository)
    {
        // SECURITY: Extract userId from JWT, NOT from request
        var userId = context.User.GetUserId();

        if (userId is null)
            return Results.Unauthorized();

        var discussionIdValue = DiscussionId.From(discussionId);
        var postIdValue = PostId.From(postId);
        var readState = await readStateRepository.GetAsync(userId, discussionIdValue);

        if (readState is null)
            readState = DiscussionReadState.Create(userId, discussionIdValue, postIdValue);
        else
            readState.MarkAsRead(postIdValue);

        await readStateRepository.SaveAsync(readState);

        return TypedResults.Ok(new SuccessResponse(true));
    }

    private static async Task<IResult> BatchMarkAsReadAsync(
        BatchMarkAsReadRequest request,
        HttpContext httpContext,
        IDiscussionReadStateRepository readStateRepository)
    {
        if (request?.Updates is null || request.Updates.Count == 0)
            return TypedResults.Ok(new SuccessResponse(true, 0));

        // SECURITY: Extract userId from JWT
        var userId = httpContext.User.GetUserId();

        if (userId is null)
            return Results.Unauthorized();

        var processed = 0;

        foreach (var update in request.Updates)
        {
            try
            {
                var discussionIdValue = DiscussionId.From(update.DiscussionId);
                var postIdValue = PostId.From(update.PostId);
                var readState = await readStateRepository.GetAsync(userId, discussionIdValue);

                if (readState is null)
                    readState = DiscussionReadState.Create(userId, discussionIdValue, postIdValue);
                else
                    readState.MarkAsRead(postIdValue);

                await readStateRepository.SaveAsync(readState);
                processed++;
            }
            catch
            {
                // Continue processing other updates even if one fails
                continue;
            }
        }

        return TypedResults.Ok(new SuccessResponse(true, processed));
    }
}

public record BatchMarkAsReadRequest(List<ReadStateUpdateDto> Updates);
public record ReadStateUpdateDto(string DiscussionId, string PostId, long Timestamp);
