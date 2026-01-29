namespace Snakk.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Snakk.Api.Models;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
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
        SnakkDbContext dbContext)
    {
        // Get the post's creation timestamp
        var post = await dbContext.Posts
            .AsNoTracking()
            .Where(p => p.PublicId == postId && p.Discussion.PublicId == discussionId)
            .Select(p => new { p.CreatedAt })
            .FirstOrDefaultAsync();

        if (post == null)
            return Results.NotFound();

        // Count all non-deleted posts created before or at this timestamp
        var postNumber = await dbContext.Posts
            .AsNoTracking()
            .Where(p => p.Discussion.PublicId == discussionId &&
                       !p.IsDeleted &&
                       p.CreatedAt <= post.CreatedAt)
            .CountAsync();

        return Results.Ok(new { postNumber });
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
        SnakkDbContext dbContext,
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null)
    {
        var today = DateTime.UtcNow.Date;

        var postsQuery = dbContext.Posts
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.CreatedAt >= today);

        // Filter by community if specified
        if (!string.IsNullOrEmpty(communityId))
        {
            postsQuery = postsQuery
                .Where(p => p.Discussion.Space.Hub.Community.PublicId == communityId);
        }

        // Filter by space if specified (most specific)
        if (!string.IsNullOrEmpty(spaceId))
        {
            postsQuery = postsQuery
                .Where(p => p.Discussion.Space.PublicId == spaceId);
        }
        // Filter by hub if specified
        else if (!string.IsNullOrEmpty(hubId))
        {
            postsQuery = postsQuery
                .Where(p => p.Discussion.Space.Hub.PublicId == hubId);
        }

        var topDiscussions = await postsQuery
            .GroupBy(p => p.DiscussionId)
            .Select(g => new { DiscussionId = g.Key, PostCountToday = g.Count() })
            .OrderByDescending(x => x.PostCountToday)
            .Take(5)
            .Join(
                dbContext.Discussions.AsNoTracking().Where(d => !d.IsDeleted),
                x => x.DiscussionId,
                d => d.Id,
                (x, d) => new { Discussion = d, x.PostCountToday })
            .Select(x => new
            {
                publicId = x.Discussion.PublicId,
                title = x.Discussion.Title,
                slug = x.Discussion.Slug,
                postCountToday = x.PostCountToday,
                space = new
                {
                    publicId = x.Discussion.Space.PublicId,
                    slug = x.Discussion.Space.Slug,
                    name = x.Discussion.Space.Name
                },
                hub = new
                {
                    publicId = x.Discussion.Space.Hub.PublicId,
                    slug = x.Discussion.Space.Hub.Slug,
                    name = x.Discussion.Space.Hub.Name
                },
                author = new
                {
                    publicId = x.Discussion.CreatedByUser.PublicId,
                    displayName = x.Discussion.CreatedByUser.DisplayName
                }
            })
            .ToListAsync();

        return Results.Ok(new { items = topDiscussions });
    }

    private static async Task<IResult> GetDiscussionPostsAsync(
        string discussionId,
        int offset,
        int pageSize,
        PostUseCase useCase,
        ReactionUseCase reactionUseCase,
        Snakk.Domain.Repositories.IUserRepository userRepository,
        HttpContext httpContext)
    {
        var result = await useCase.GetPostsByDiscussionAsync(
            DiscussionId.From(discussionId),
            offset,
            pageSize);

        // Filter out soft-deleted posts first
        var visiblePosts = result.Items.Where(p => !p.IsDeleted).ToList();

        // Fetch all unique authors for this page of posts in a single query
        var authorIds = visiblePosts.Select(p => p.CreatedByUserId).Distinct().ToList();
        var authorUsers = await userRepository.GetByPublicIdsAsync(authorIds);
        var authorsDict = authorUsers.ToDictionary(u => u.PublicId.Value);

        var authors = new Dictionary<string, (string DisplayName, string? Role, string? AvatarFileName, bool IsDeleted)>();
        foreach (var authorId in authorIds)
        {
            if (authorsDict.TryGetValue(authorId.Value, out var user))
            {
                authors[authorId.Value] = (user.DisplayName, user.Role, user.AvatarFileName, false);
            }
            else
            {
                authors[authorId.Value] = ("Deleted User", null, null, true);
            }
        }

        // Fetch reply-to posts if any (for quoted snippets)
        var replyToIds = visiblePosts
            .Where(p => p.ReplyToPostId != null)
            .Select(p => p.ReplyToPostId!)
            .Distinct()
            .ToList();

        var replyToPosts = new Dictionary<string, (string AuthorName, string ContentSnippet)>();

        // Batch fetch all reply posts
        var replyPostsList = (await useCase.GetPostsByPublicIdsAsync(replyToIds))
            .Where(p => !p.IsDeleted)
            .ToList();

        // Batch fetch all reply post authors
        var replyAuthorIds = replyPostsList.Select(p => p.CreatedByUserId).Distinct().ToList();
        var replyAuthorUsers = await userRepository.GetByPublicIdsAsync(replyAuthorIds);
        var replyAuthorsDict = replyAuthorUsers.ToDictionary(u => u.PublicId.Value);

        // Build reply-to dictionary
        foreach (var replyPost in replyPostsList)
        {
            var authorName = replyAuthorsDict.TryGetValue(replyPost.CreatedByUserId.Value, out var replyAuthor)
                ? replyAuthor.DisplayName
                : "Deleted User";
            var snippet = replyPost.Content.Length > 100
                ? replyPost.Content.Substring(0, 100) + "..."
                : replyPost.Content;
            replyToPosts[replyPost.PublicId.Value] = (authorName, snippet);
        }

        // Fetch reactions in batch for all posts
        var postIds = visiblePosts.Select(p => p.PublicId).ToList();
        var reactionCounts = await reactionUseCase.GetReactionCountsBatchAsync(postIds);

        // Fetch current user's reactions if authenticated
        var userReactions = new Dictionary<string, ReactionType>();
        var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim != null)
        {
            userReactions = await reactionUseCase.GetUserReactionsBatchAsync(
                UserId.From(userIdClaim.Value), postIds);
        }

        return Results.Ok(new
        {
            items = visiblePosts.Select((p, index) => new
            {
                postNumber = offset + index + 1,
                publicId = p.PublicId.Value,
                content = p.Content,
                createdAt = p.CreatedAt,
                editedAt = p.EditedAt,
                isFirstPost = p.IsFirstPost,
                isDeleted = p.IsDeleted,
                createdByUserId = p.CreatedByUserId.Value,
                author = new
                {
                    publicId = p.CreatedByUserId.Value,
                    displayName = authors[p.CreatedByUserId.Value].DisplayName,
                    avatarUrl = $"/avatars/{p.CreatedByUserId.Value}",
                    role = authors[p.CreatedByUserId.Value].Role,
                    isDeleted = authors[p.CreatedByUserId.Value].IsDeleted
                },
                replyTo = p.ReplyToPostId != null && replyToPosts.ContainsKey(p.ReplyToPostId.Value)
                    ? new
                    {
                        postId = p.ReplyToPostId.Value,
                        authorName = replyToPosts[p.ReplyToPostId.Value].AuthorName,
                        contentSnippet = replyToPosts[p.ReplyToPostId.Value].ContentSnippet
                    }
                    : null,
                reactions = new
                {
                    counts = reactionCounts.TryGetValue(p.PublicId.Value, out var counts)
                        ? new
                        {
                            thumbsUp = counts.GetValueOrDefault(ReactionType.ThumbsUp, 0),
                            heart = counts.GetValueOrDefault(ReactionType.Heart, 0),
                            eyes = counts.GetValueOrDefault(ReactionType.Eyes, 0)
                        }
                        : new { thumbsUp = 0, heart = 0, eyes = 0 },
                    userReaction = userReactions.TryGetValue(p.PublicId.Value, out var userReaction)
                        ? userReaction.ToString()
                        : null
                }
            }),
            offset = result.Offset,
            pageSize = result.PageSize,
            hasMoreItems = result.HasMoreItems
        });
    }
}
