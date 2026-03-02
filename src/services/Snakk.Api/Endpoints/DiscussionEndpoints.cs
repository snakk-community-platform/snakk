namespace Snakk.Api.Endpoints;

using Snakk.Api.Models;
using Snakk.Api.Extensions;
using Snakk.Application.UseCases;
using Snakk.Application.DTOs.Responses;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Api.Filters;
using Snakk.Shared.Helpers;

public static class DiscussionEndpoints
{
    public static void MapDiscussionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/discussions")
            .WithTags("Discussions");

        group.MapPost("/", CreateDiscussionAsync)
            .WithName("CreateDiscussion")
            .Produces<DiscussionCreatedResponse>(StatusCodes.Status201Created)
            .RequireAuthorization()
            .RequireRateLimiting("api")
            .AddEndpointFilter<ValidationFilter<CreateDiscussionRequest>>();

        group.MapGet("/{publicId}", GetDiscussionAsync)
            .WithName("GetDiscussion")
            .Produces<DiscussionResponse>();

        group.MapGet("/recent", GetRecentDiscussionsAsync)
            .WithName("GetRecentDiscussions")
            .Produces<PagedResponse<RecentDiscussionResponse>>();

        group.MapGet("/top-active-today", GetTopActiveDiscussionsTodayAsync)
            .WithName("GetTopActiveDiscussionsToday")
            .Produces<TopActiveDiscussionsResponse>();

        group.MapGet("/{discussionId}/posts", GetDiscussionPostsAsync)
            .WithName("GetDiscussionPosts")
            .Produces<PagedResponse<EnrichedPostResponse>>();

        group.MapGet("/{discussionId}/posts/{postId}/number", GetPostNumberAsync)
            .WithName("GetPostNumber")
            .Produces<PostNumberResponse>();

        group.MapGet("/{discussionId}/preview", GetDiscussionPreviewAsync)
            .WithName("GetDiscussionPreview")
            .Produces<DiscussionPreviewResponse>();

        group.MapGet("/{publicId}/stats", GetDiscussionStatsAsync)
            .WithName("GetDiscussionStats")
            .Produces<DiscussionStatsResponse>();
    }

    private static async Task<IResult> CreateDiscussionAsync(
        CreateDiscussionRequest request,
        DiscussionUseCase useCase,
        HttpContext context)
    {
        // SECURITY: Extract userId from JWT, NOT from request
        var userId = context.User.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var result = await useCase.CreateDiscussionAsync(
            SpaceId.From(request.SpaceId),
            userId,
            request.Title,
            request.Slug,
            request.FirstPostContent);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return TypedResults.Created($"/discussions/{result.Value!.PublicId}", new DiscussionCreatedResponse(
            PublicId: result.Value.PublicId.Value,
            Title: result.Value.Title,
            Slug: result.Value.Slug,
            CreatedAt: result.Value.CreatedAt));
    }

    private static async Task<IResult> GetDiscussionAsync(
        string publicId,
        DiscussionUseCase useCase)
    {
        var result = await useCase.GetDiscussionAsync(DiscussionId.From(publicId));

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return TypedResults.Ok(new DiscussionResponse(
            PublicId: result.Value!.PublicId.Value,
            Title: result.Value.Title,
            Slug: result.Value.Slug,
            SpaceId: result.Value.SpaceId.Value,
            CreatedAt: result.Value.CreatedAt,
            LastActivityAt: result.Value.LastActivityAt,
            IsPinned: result.Value.IsPinned,
            IsLocked: result.Value.IsLocked));
    }

    private static async Task<IResult> GetPostNumberAsync(
        string discussionId,
        string postId,
        DiscussionUseCase useCase)
    {
        var result = await useCase.GetPostNumberAsync(
            DiscussionId.From(discussionId),
            PostId.From(postId));

        if (!result.IsSuccess)
            return Results.NotFound();

        return TypedResults.Ok(new PostNumberResponse(
            PostNumber: result.Value));
    }

    private static async Task<IResult> GetDiscussionPreviewAsync(
        string discussionId,
        DiscussionUseCase useCase)
    {
        var result = await useCase.GetFirstPostPreviewAsync(DiscussionId.From(discussionId));

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return TypedResults.Ok(new DiscussionPreviewResponse(
            Content: result.Value!));
    }

    private static async Task<IResult> GetRecentDiscussionsAsync(
        HttpContext httpContext,
        int offset,
        int pageSize,
        Application.Repositories.ISearchRepository searchRepo,
        string? communityId = null,
        string? cursor = null)
    {
        // Clamp pagination parameters
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);
        offset = Math.Max(0, offset);

        var result = await searchRepo.GetRecentDiscussionsAsync(offset, pageSize, communityId, cursor);

        return TypedResults.Ok(new PagedResponse<RecentDiscussionResponse>(
            Items: result.Items.Select(d => new RecentDiscussionResponse(
                PublicId: d.PublicId,
                Title: d.Title,
                Slug: d.Slug,
                CreatedAt: d.CreatedAt,
                LastActivityAt: d.LastActivityAt,
                IsPinned: d.IsPinned,
                IsLocked: d.IsLocked,
                Space: new EntityRef(
                    PublicId: d.SpacePublicId,
                    Slug: d.SpaceSlug,
                    Name: d.SpaceName,
                    AvatarUrl: AvatarHelper.GetAvatarUrl(d.SpacePublicId, AvatarEntityType.Space, 0)),
                Hub: new EntityRef(
                    PublicId: d.HubPublicId,
                    Slug: d.HubSlug,
                    Name: d.HubName,
                    AvatarUrl: AvatarHelper.GetAvatarUrl(d.HubPublicId, AvatarEntityType.Hub, 0)),
                Community: new EntityRef(
                    PublicId: d.CommunityPublicId,
                    Slug: d.CommunitySlug,
                    Name: d.CommunityName,
                    AvatarUrl: AvatarHelper.GetAvatarUrl(d.CommunityPublicId, AvatarEntityType.Community, 0)),
                Author: new AuthorRef(
                    PublicId: d.CreatedByUserPublicId,
                    DisplayName: d.CreatedByUserDisplayName,
                    AvatarUrl: AvatarHelper.GetAvatarUrl(d.CreatedByUserPublicId, AvatarEntityType.User, 0)),
                PostCount: d.PostCount,
                ReactionCount: d.ReactionCount,
                Tags: d.Tags)),
            Offset: result.Offset,
            PageSize: result.PageSize,
            HasMoreItems: result.HasMoreItems,
            NextCursor: result.NextCursor));
    }

    private static async Task<IResult> GetTopActiveDiscussionsTodayAsync(
        StatisticsUseCase useCase,
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null)
    {
        var result = await useCase.GetTopActiveDiscussionsTodayAsync(
            hubId,
            spaceId,
            communityId,
            limit: 5);

        if (!result.IsSuccess)
            return Results.Problem(result.Error);

        return TypedResults.Ok(new TopActiveDiscussionsResponse(
            Items: result.Value!.Items.Select(d => new TopActiveDiscussionResponse(
                PublicId: d.DiscussionId,
                Title: d.Title,
                Slug: d.Slug,
                PostCountToday: d.PostCountToday,
                Space: new EntityRef(
                    PublicId: d.SpacePublicId,
                    Slug: d.SpaceSlug,
                    Name: d.SpaceName),
                Hub: new EntityRef(
                    PublicId: d.HubPublicId,
                    Slug: d.HubSlug,
                    Name: d.HubName),
                Author: new AuthorRef(
                    PublicId: d.AuthorPublicId,
                    DisplayName: d.AuthorDisplayName)))));
    }

    private static async Task<IResult> GetDiscussionPostsAsync(
        string discussionId,
        int offset,
        int pageSize,
        PostUseCase useCase,
        Snakk.Api.Services.ICurrentUserService currentUser)
    {
        // Clamp pagination parameters
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);
        offset = Math.Max(0, offset);

        // Get current user ID
        var userId = currentUser.GetCurrentUserId();
        var currentUserId = userId != null ? UserId.From(userId) : null;

        // Call use case
        var result = await useCase.GetEnrichedPostsByDiscussionAsync(
            DiscussionId.From(discussionId),
            currentUserId,
            offset,
            pageSize);

        if (!result.IsSuccess)
            return Results.NotFound();

        // Map to typed DTOs
        var data = result.Value!;
        return TypedResults.Ok(new PagedResponse<EnrichedPostResponse>(
            Items: data.Posts.Select(p => new EnrichedPostResponse(
                PostNumber: p.PostNumber,
                PublicId: p.Post.PublicId.Value,
                Content: p.Post.Content,
                CreatedAt: p.Post.CreatedAt,
                EditedAt: p.Post.EditedAt,
                IsFirstPost: p.Post.IsFirstPost,
                IsDeleted: p.Post.IsDeleted,
                CreatedByUserId: p.Post.CreatedByUserId.Value,
                Author: new AuthorRef(
                    PublicId: p.Post.CreatedByUserId.Value,
                    DisplayName: p.Author.DisplayName,
                    AvatarUrl: AvatarHelper.GetAvatarUrl(p.Post.CreatedByUserId.Value, AvatarEntityType.User, p.Author.AvatarRevision),
                    Role: p.Author.Role,
                    IsDeleted: p.Author.IsDeleted),
                ReplyTo: p.ReplyTo != null ? new ReplyToRef(
                    AuthorName: p.ReplyTo.AuthorName,
                    ContentSnippet: p.ReplyTo.ContentSnippet) : null,
                Reactions: new PostReactionsResponse(
                    Counts: new ReactionCountsResponse(
                        ThumbsUp: p.ReactionCounts.GetValueOrDefault(ReactionType.ThumbsUp, 0),
                        Heart: p.ReactionCounts.GetValueOrDefault(ReactionType.Heart, 0),
                        Eyes: p.ReactionCounts.GetValueOrDefault(ReactionType.Eyes, 0),
                        Crazy: p.ReactionCounts.GetValueOrDefault(ReactionType.Crazy, 0)),
                    UserReaction: p.UserReaction?.ToString()))),
            Offset: data.Offset,
            PageSize: data.PageSize,
            HasMoreItems: data.HasMoreItems));
    }

    private static async Task<IResult> GetDiscussionStatsAsync(string publicId, StatisticsUseCase useCase)
    {
        var result = await useCase.GetDiscussionStatsAsync(publicId);

        if (!result.IsSuccess)
            return Results.NotFound();

        var stats = result.Value!;
        return TypedResults.Ok(new DiscussionStatsResponse(
            PublicId: stats.PublicId,
            Title: stats.Title,
            ReplyCount: stats.ReplyCount,
            FollowerCount: stats.FollowerCount));
    }
}
