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
    ICurrentUserService currentUser) : PostService.PostServiceBase
{
    public override async Task<PagedEnrichedPostList> GetPostsByDiscussion(GetPostsByDiscussionRequest request, ServerCallContext context)
    {
        UserId? currentUserId = null;

        if (currentUser.IsAuthenticated())
        {
            var uid = currentUser.GetCurrentUserId();

            if (uid is not null) currentUserId = UserId.From(uid);
        }

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
                        ep.Author.AvatarRevision),
                    Role = ep.Author.Role ?? "",
                    IsDeleted = ep.Author.IsDeleted
                },
                HasCodeBlock = post.HasCodeBlock,
                Reactions = new PostReactions
                {
                    Counts = new ReactionCounts
                    {
                        ThumbsUp = ep.ReactionCounts.GetValueOrDefault(Snakk.Domain.ValueObjects.ReactionType.ThumbsUp, 0),
                        Heart = ep.ReactionCounts.GetValueOrDefault(Snakk.Domain.ValueObjects.ReactionType.Heart, 0),
                        Eyes = ep.ReactionCounts.GetValueOrDefault(Snakk.Domain.ValueObjects.ReactionType.Eyes, 0),
                        Crazy = ep.ReactionCounts.GetValueOrDefault(Snakk.Domain.ValueObjects.ReactionType.Crazy, 0)
                    },
                    UserReaction = ep.UserReaction?.ToString() ?? ""
                }
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
        var userId = RequireAuth();

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

    private UserId RequireAuth()
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userId = currentUser.GetCurrentUserId();

        if (userId is null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        return UserId.From(userId);
    }

    private static Timestamp ToTimestamp(DateTime dt) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}
