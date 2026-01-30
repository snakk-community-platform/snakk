namespace Snakk.Api.Endpoints;

using Snakk.Api.Models;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Repositories;

public static class DiscussionEndpoints
{
    public static void MapDiscussionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/discussions")
            .WithTags("Discussions");

        group.MapPost("/", CreateDiscussionAsync)
            .WithName("CreateDiscussion");

        group.MapGet("/{publicId}", GetDiscussionAsync)
            .WithName("GetDiscussion");

        group.MapGet("/recent", GetRecentDiscussionsAsync)
            .WithName("GetRecentDiscussions");

        group.MapGet("/top-active-today", GetTopActiveDiscussionsTodayAsync)
            .WithName("GetTopActiveDiscussionsToday");

        group.MapGet("/{discussionId}/posts", GetDiscussionPostsAsync)
            .WithName("GetDiscussionPosts");

        group.MapGet("/{discussionId}/posts/{postId}/number", GetPostNumberAsync)
            .WithName("GetPostNumber");
    }

    private static async Task<IResult> CreateDiscussionAsync(
        CreateDiscussionRequest request,
        DiscussionUseCase useCase)
    {
        var result = await useCase.CreateDiscussionAsync(
            SpaceId.From(request.SpaceId),
            UserId.From(request.UserId),
            request.Title,
            request.Slug,
            request.FirstPostContent);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Created($"/discussions/{result.Value!.PublicId}", new
        {
            publicId = result.Value.PublicId.Value,
            title = result.Value.Title,
            slug = result.Value.Slug,
            createdAt = result.Value.CreatedAt
        });
    }

    private static async Task<IResult> GetDiscussionAsync(
        string publicId,
        DiscussionUseCase useCase)
    {
        var result = await useCase.GetDiscussionAsync(DiscussionId.From(publicId));

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return Results.Ok(new
        {
            publicId = result.Value!.PublicId.Value,
            title = result.Value.Title,
            slug = result.Value.Slug,
            spaceId = result.Value.SpaceId.Value,
            createdAt = result.Value.CreatedAt,
            lastActivityAt = result.Value.LastActivityAt,
            isPinned = result.Value.IsPinned,
            isLocked = result.Value.IsLocked
        });
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

        return Results.Ok(new { postNumber = result.Value });
    }

    private static async Task<IResult> GetRecentDiscussionsAsync(
        HttpContext httpContext,
        int offset,
        int pageSize,
        IDiscussionRepository discussionRepo,
        string? communityId = null,
        string? cursor = null)
    {
        var result = await discussionRepo.GetRecentWithDetailsAsync(offset, pageSize, communityId, cursor);

        return Results.Ok(new
        {
            items = result.Items.Select(d => new
            {
                publicId = d.PublicId,
                title = d.Title,
                slug = d.Slug,
                createdAt = d.CreatedAt,
                lastActivityAt = d.LastActivityAt,
                isPinned = d.IsPinned,
                isLocked = d.IsLocked,
                space = new
                {
                    publicId = d.SpacePublicId,
                    slug = d.SpaceSlug,
                    name = d.SpaceName
                },
                hub = new
                {
                    publicId = d.HubPublicId,
                    slug = d.HubSlug,
                    name = d.HubName
                },
                community = new
                {
                    publicId = d.CommunityPublicId,
                    slug = d.CommunitySlug,
                    name = d.CommunityName
                },
                author = new
                {
                    publicId = d.CreatedByUserPublicId,
                    displayName = d.CreatedByUserDisplayName,
                    avatarFileName = d.CreatedByUserAvatarFileName
                },
                postCount = d.PostCount,
                reactionCount = d.ReactionCount,
                tags = d.Tags
            }),
            offset = result.Offset,
            pageSize = result.PageSize,
            hasMoreItems = result.HasMoreItems,
            nextCursor = result.NextCursor
        });
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

        return Results.Ok(new
        {
            items = result.Value!.Items.Select(d => new
            {
                discussionId = d.DiscussionId,
                title = d.Title,
                slug = d.Slug,
                postCountToday = d.PostCountToday,
                spaceName = d.SpaceName,
                hubName = d.HubName
            })
        });
    }

    private static async Task<IResult> GetDiscussionPostsAsync(
        string discussionId,
        int offset,
        int pageSize,
        PostUseCase useCase,
        Snakk.Api.Services.ICurrentUserService currentUser)
    {
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

        // Map to DTO (endpoint's responsibility)
        var data = result.Value!;
        return Results.Ok(new
        {
            items = data.Posts.Select(p => new
            {
                postNumber = p.PostNumber,
                publicId = p.Post.PublicId.Value,
                content = p.Post.Content,
                createdAt = p.Post.CreatedAt,
                editedAt = p.Post.EditedAt,
                isFirstPost = p.Post.IsFirstPost,
                isDeleted = p.Post.IsDeleted,
                createdByUserId = p.Post.CreatedByUserId.Value,
                author = new
                {
                    publicId = p.Post.CreatedByUserId.Value,
                    displayName = p.Author.DisplayName,
                    avatarUrl = $"/avatars/{p.Post.CreatedByUserId.Value}",
                    role = p.Author.Role,
                    avatarFileName = p.Author.AvatarFileName,
                    isDeleted = p.Author.IsDeleted
                },
                replyTo = p.ReplyTo != null ? new
                {
                    authorName = p.ReplyTo.AuthorName,
                    contentSnippet = p.ReplyTo.ContentSnippet
                } : null,
                reactions = new
                {
                    counts = new
                    {
                        thumbsUp = p.ReactionCounts.GetValueOrDefault(ReactionType.ThumbsUp, 0),
                        heart = p.ReactionCounts.GetValueOrDefault(ReactionType.Heart, 0),
                        eyes = p.ReactionCounts.GetValueOrDefault(ReactionType.Eyes, 0)
                    },
                    userReaction = p.UserReaction?.ToString()
                }
            }),
            offset = data.Offset,
            pageSize = data.PageSize,
            hasMoreItems = data.HasMoreItems
        });
    }
}
