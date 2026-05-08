namespace Snakk.Api.Endpoints;

using Snakk.Application.DTOs.Responses;
using Snakk.Application.Services;
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
        IDiscussionReadStateRepository readStateRepository,
        IRealtimeNotifier realtimeNotifier)
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
        await realtimeNotifier.NotifyReadStateUpdatedAsync(UserId.From(userId), discussionId, postId);

        return TypedResults.Ok(new SuccessResponse(true));
    }

    private static async Task<IResult> BatchMarkAsReadAsync(
        BatchMarkAsReadRequest request,
        HttpContext httpContext,
        IDiscussionReadStateRepository readStateRepository,
        IRealtimeNotifier realtimeNotifier)
    {
        if (request?.Updates is null || request.Updates.Count == 0)
            return TypedResults.Ok(new SuccessResponse(true, 0));

        // SECURITY: Extract userId from JWT
        var userId = httpContext.User.GetUserId();

        if (userId is null)
            return Results.Unauthorized();

        var userIdValue = UserId.From(userId);

        var updatedStates = request.Updates
            .Select(u => DiscussionReadState.Create(userIdValue, DiscussionId.From(u.DiscussionId), PostId.From(u.PostId)))
            .ToList();

        await readStateRepository.BatchSaveAsync(updatedStates);

        await Task.WhenAll(request.Updates.Select(u =>
            realtimeNotifier.NotifyReadStateUpdatedAsync(userIdValue, u.DiscussionId, u.PostId)));

        return TypedResults.Ok(new SuccessResponse(true, request.Updates.Count));
    }
}

public record BatchMarkAsReadRequest(List<ReadStateUpdateDto> Updates);
public record ReadStateUpdateDto(string DiscussionId, string PostId, long Timestamp);
