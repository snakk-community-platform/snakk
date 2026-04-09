using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Shared.Helpers;
using Snakk.Api.Services;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Protos;
using Snakk.Protos.Post;
using Snakk.Shared.Enums;

namespace Snakk.Api.GrpcServices;

public class PostGrpcService(
    PostUseCase postUseCase,
    DiscussionUseCase discussionUseCase,
    IMarkupParser markupParser,
    ICurrentUserService currentUser,
    IUserGrantsCacheService grantsCache,
    IEntityHierarchyCacheService hierarchyCache,
    IGroupAccessService groupAccessService) : PostService.PostServiceBase
{
    public override async Task<PagedEnrichedPostList> GetPostsByDiscussion(GetPostsByDiscussionRequest request, ServerCallContext context)
    {
        UserId? currentUserId = null;
        string? currentUserIdValue = null;

        if (currentUser.IsAuthenticated())
        {
            currentUserIdValue = currentUser.GetCurrentUserId();

            if (currentUserIdValue is not null) currentUserId = UserId.From(currentUserIdValue);
        }

        if (!await IsDiscussionAccessibleAsync(request.DiscussionId, currentUserIdValue))
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        var result = await postUseCase.GetEnrichedPostsByDiscussionAsync(
            DiscussionId.From(request.DiscussionId),
            currentUserId,
            request.Offset,
            request.PageSize);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        var data = result.Value;
        var response = new PagedEnrichedPostList
        {
            Offset = data.Offset,
            PageSize = data.PageSize,
            HasMoreItems = data.HasMoreItems
        };

        response.HasCodeBlocks = data.Posts.Any(ep => ep.Post.HasCodeBlock);

        foreach (var ep in data.Posts)
        {
            var post = ep.Post;
            var item = new EnrichedPostInfo
            {
                PostNumber = ep.PostNumber,
                PublicId = post.PublicId.Value,
                Content = post.Content,
                RenderedContent = post.IsDeleted ? "" : post.RenderedContent,
                CreatedAt = ToTimestamp(post.CreatedAt),
                IsFirstPost = post.IsFirstPost,
                IsDeleted = post.IsDeleted,
                CreatedByUserId = post.CreatedByUserId.Value,

                Author = new AuthorRef
                {
                    PublicId = post.CreatedByUserId.Value,
                    DisplayName = ep.Author.DisplayName,
                    AvatarUrl = AvatarHelper.GetAvatarUrl(
                        post.CreatedByUserId.Value,
                        AvatarEntityType.User,
                        ep.Author.AvatarRevision,
                        ep.Author.AvatarFileName),
                    AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(
                        post.CreatedByUserId.Value,
                        AvatarEntityType.User,
                        ep.Author.AvatarRevision,
                        ep.Author.AvatarFileName,
                        ep.Author.AvatarThumbnailFileName),
                    AvatarMicroUrl = AvatarHelper.GetAvatarMicroUrl(
                        post.CreatedByUserId.Value,
                        AvatarEntityType.User,
                        ep.Author.AvatarRevision,
                        ep.Author.AvatarFileName,
                        ep.Author.AvatarMicroFileName),
                    Role = ep.Author.Role ?? "",
                    IsDeleted = ep.Author.IsDeleted,
                    JoinedAt = ToTimestamp(ep.Author.JoinedAt),
                    DiscussionCount = ep.Author.DiscussionCount,
                    ReplyCount = ep.Author.ReplyCount
                },
                HasCodeBlock = post.HasCodeBlock,
                IsUsersFirstPostInDiscussion = post.IsUsersFirstPostInDiscussion,
                IsUsersFirstPostInSpace = post.IsUsersFirstPostInSpace,
                IsOp = post.IsOp,
                IsNecro = post.IsNecro,
                IsMilestone = post.IsMilestone,
                Reactions = BuildPostReactions(ep.ReactionCounts, ep.UserReactions)
            };

            if (post.EditedAt.HasValue)
                item.EditedAt = ToTimestamp(post.EditedAt.Value);

            if (ep.ReplyTo is not null)
            {
                item.ReplyTo = new ReplyToRef
                {
                    AuthorName = ep.ReplyTo.AuthorName,
                    ContentSnippet = ep.ReplyTo.ContentSnippet
                };
            }

            response.Items.Add(item);
        }

        return response;
    }

    public override async Task<PostCreatedInfo> CreatePost(CreatePostRequest request, ServerCallContext context)
    {
        if (request.Content?.Length > 50000)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Content must be 50,000 characters or less"));

        var userId = RequireAuth();

        if (!await IsDiscussionAccessibleAsync(request.DiscussionId, userId.Value))
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        // Enforce write permissions:
        // Write = can reply to any discussion
        // Author = can only reply to own discussions
        var (access, authorId) = await groupAccessService.CheckAccessForDiscussionAsync(
            userId.Value, request.DiscussionId, context.CancellationToken);

        if (!access.CanWrite && !(access.CanAuthor && authorId == userId.Value))
            throw new RpcException(new Status(StatusCode.PermissionDenied, "You don't have permission to reply here"));

        PostId? replyToPostId = request.HasReplyToPostId
            ? PostId.From(request.ReplyToPostId)
            : null;

        var result = await postUseCase.CreatePostAsync(
            DiscussionId.From(request.DiscussionId),
            userId,
            request.Content,
            replyToPostId);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to create post"));

        var post = result.Value;

        return new PostCreatedInfo
        {
            PublicId = post.PublicId.Value,
            Content = post.Content,
            CreatedAt = ToTimestamp(post.CreatedAt),
            DiscussionId = post.DiscussionId.Value
        };
    }

    public override async Task<EditPostResponse> EditPost(EditPostRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();

        var result = await postUseCase.UpdatePostAsync(
            PostId.From(request.PostId),
            userId,
            request.Content);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to edit post"));

        var post = result.Value;

        return new EditPostResponse
        {
            RenderedHtml = post.RenderedContent
        };
    }

    public override async Task<PostNumberResponse> GetPostNumber(GetPostNumberRequest request, ServerCallContext context)
    {
        var result = await discussionUseCase.GetPostNumberAsync(
            DiscussionId.From(request.DiscussionId),
            PostId.From(request.PostId));

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.NotFound, "Post not found"));

        return new PostNumberResponse { PostNumber = result.Value };
    }

    public override async Task<PostHistoryResponse> GetPostHistory(GetPostHistoryRequest request, ServerCallContext context)
    {
        RequireAuth();

        var revisions = await postUseCase.GetPostHistoryAsync(PostId.From(request.PostId));

        var response = new PostHistoryResponse();
        var htmlParts = new List<string>();

        foreach (var revision in revisions)
        {
            htmlParts.Add(markupParser.ToHtml(revision.Content));
        }

        response.RenderedHtml = string.Join("\n---\n", htmlParts);

        return response;
    }

    private async Task<bool> IsDiscussionAccessibleAsync(string discussionPublicId, string? userId)
    {
        var restricted = await grantsCache.GetRestrictedEntitiesAsync();
        if (restricted.IsEmpty) return true;

        var h = await hierarchyCache.GetDiscussionHierarchyAsync(discussionPublicId);

        if (h is null) return false;

        var spaceGate = restricted.SpaceIds.Contains(h.SpaceId);
        var hubGate = restricted.HubIds.Contains(h.HubId);
        var communityGate = restricted.CommunityIds.Contains(h.CommunityId);

        if (!spaceGate && !hubGate && !communityGate) return true;
        if (userId is null) return false;

        var grants = await grantsCache.GetGrantsAsync(userId);
        return (!spaceGate || grants.SpaceIds.Contains(h.SpaceId))
            && (!hubGate || grants.HubIds.Contains(h.HubId))
            && (!communityGate || grants.CommunityIds.Contains(h.CommunityId));
    }

    private UserId RequireAuth()
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userId = currentUser.GetCurrentUserId();

        if (userId is null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        return UserId.From(userId);
    }

    private static PostReactions BuildPostReactions(
        Dictionary<Snakk.Domain.ValueObjects.ReactionType, int> reactionCounts,
        List<Snakk.Domain.ValueObjects.ReactionType> userReactions)
    {
        var counts = new ReactionCounts();

        foreach (var kvp in reactionCounts)
            counts.Counts[kvp.Key.ToString()] = kvp.Value;

        var reactions = new PostReactions { Counts = counts };
        var ur = new UserReactions();
        ur.Reactions.AddRange(userReactions.Select(r => r.ToString()));
        reactions.UserReactions = ur;

        return reactions;
    }

    private static Timestamp ToTimestamp(DateTime dt) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}
