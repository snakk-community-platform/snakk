namespace Snakk.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Snakk.Api.Extensions;
using Snakk.Api.Models;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Domain.Repositories;
using Snakk.Api.Filters;

public static class PostEndpoints
{
    public static void MapPostEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/posts")
            .WithTags("Posts");

        // All post endpoints require authentication
        group.MapPost("/", CreatePostAsync)
            .WithName("CreatePost")
            .RequireAuthorization()
            .RequireRateLimiting("api")
            .AddEndpointFilter<ValidationFilter<CreatePostRequest>>();

        // API endpoints under /api/posts
        var apiGroup = app.MapGroup("/api/posts")
            .WithTags("Posts API")
            .RequireAuthorization();

        apiGroup.MapPost("/{publicId}/edit", EditPostHtmxAsync)
            .WithName("EditPostHtmx")
            .RequireRateLimiting("api");

        apiGroup.MapDelete("/{publicId}", DeletePostHtmxAsync)
            .WithName("DeletePostHtmx")
            .RequireRateLimiting("api");

        apiGroup.MapGet("/{publicId}/history", GetPostHistoryHtmxAsync)
            .WithName("GetPostHistoryHtmx");
    }

    private static async Task<IResult> CreatePostAsync(
        CreatePostRequest request,
        PostUseCase useCase,
        HttpContext context)
    {
        // SECURITY: Extract userId from JWT claims, NOT from request
        var userId = context.User.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var result = await useCase.CreatePostAsync(
            DiscussionId.From(request.DiscussionId),
            userId,
            request.Content,
            request.ReplyToPostId != null ? PostId.From(request.ReplyToPostId) : null);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Created($"/posts/{result.Value!.PublicId}", new
        {
            publicId = result.Value.PublicId.Value,
            content = result.Value.Content,
            createdAt = result.Value.CreatedAt,
            discussionId = result.Value.DiscussionId.Value
        });
    }

    private static async Task<IResult> EditPostHtmxAsync(
        string publicId,
        string content,
        PostUseCase useCase,
        HttpContext context,
        Snakk.Api.Services.IViewRenderingService viewService)
    {
        // SECURITY: Extract userId from JWT claims
        var userId = context.User.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var result = await useCase.UpdatePostAsync(
            PostId.From(publicId),
            userId,
            content);

        if (!result.IsSuccess)
            return Results.Content(viewService.RenderError(result.Error!), "text/html");

        var html = viewService.RenderPostCard(result.Value!, userId.Value);
        return Results.Content(html, "text/html");
    }

    private static async Task<IResult> DeletePostHtmxAsync(
        string publicId,
        PostUseCase useCase,
        HttpContext context,
        Snakk.Api.Services.IViewRenderingService viewService)
    {
        // SECURITY: Extract userId from JWT claims
        var userId = context.User.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var result = await useCase.DeletePostAsync(
            PostId.From(publicId),
            userId);

        if (!result.IsSuccess)
            return Results.Content(viewService.RenderError(result.Error!), "text/html");

        // Return empty content for hard delete, or tombstone for soft delete
        var post = await useCase.GetPostAsync(PostId.From(publicId));
        if (post.IsSuccess && post.Value!.IsDeleted)
        {
            return Results.Content(viewService.RenderDeletedPostTombstone(publicId), "text/html");
        }

        return Results.Content("", "text/html");
    }

    private static async Task<IResult> GetPostHistoryHtmxAsync(
        string publicId,
        PostUseCase useCase,
        IUserRepository userRepository,
        Snakk.Api.Services.IViewRenderingService viewService)
    {
        var revisions = await useCase.GetPostHistoryAsync(PostId.From(publicId));
        var revisionList = revisions.ToList();

        if (!revisionList.Any())
            return Results.Content("<p>No edit history available.</p>", "text/html");

        // Batch fetch all editors in one query
        var editorIds = revisionList.Select(r => r.EditedByUserId).Distinct().ToList();
        var editors = await userRepository.GetByPublicIdsAsync(editorIds);
        var authorNames = editors.ToDictionary(
            u => u.PublicId.Value,
            u => u.DisplayName);

        var html = viewService.RenderPostHistory(revisionList, authorNames);
        return Results.Content(html, "text/html");
    }
}
