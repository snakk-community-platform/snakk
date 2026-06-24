namespace Snakk.Application.UseCases;

using Snakk.Domain;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;
using Snakk.Shared.Helpers;
using Snakk.Application.Repositories;
using Snakk.Application.Services;

public class PostUseCase(
    IPostRepository postRepository,
    IDiscussionRepository discussionRepository,
    ISpaceRepository spaceRepository,
    IUserRepository userRepository,
    IFollowRepository followRepository,
    IDomainEventDispatcher eventDispatcher,
    IRealtimeNotifier realtimeNotifier,
    ICounterService counterService,
    IMediaService mediaService,
    IMarkupParser markupParser,
    IContentNormalizer contentNormalizer,
    IModerationRepository moderationRepository,
    ReactionUseCase reactionUseCase,
    IUnitOfWork unitOfWork) : UseCaseBase
{
    public async Task<Result<Post>> CreatePostAsync(
        DiscussionId discussionId,
        UserId userId,
        string content,
        PostId? replyToPostId = null)
    {
        // Validate discussion exists
        var discussion = await discussionRepository.GetByPublicIdAsync(discussionId);

        if (discussion is null)
            return Result<Post>.Failure($"Discussion '{discussionId}' not found");

        // Check if discussion is locked
        if (discussion.IsLocked)
            return Result<Post>.Failure("Cannot post to a locked discussion");

        // Validate user exists
        var user = await userRepository.GetByPublicIdAsync(userId);

        if (user is null)
            return Result<Post>.Failure($"User '{userId}' not found");

        // Check if user is banned
        var isBanned = await moderationRepository.IsUserBannedAsync(userId.Value, spacePublicId: discussion.SpaceId.Value);

        if (isBanned)
            return Result<Post>.Failure("You are currently banned from posting in this space");

        // If replying, validate reply-to post exists
        if (replyToPostId is not null)
        {
            var replyToPost = await postRepository.GetByPublicIdAsync(replyToPostId);

            if (replyToPost is null)
                return Result<Post>.Failure($"Reply-to post '{replyToPostId}' not found");
        }

        // Create post with pre-rendered HTML. Auto-paragraph is per-space.
        var space = await spaceRepository.GetByPublicIdAsync(discussion.SpaceId);
        var autoParagraph = space?.AutoParagraphEnabled ?? true;
        var (normalizedContent, wasNormalized) = contentNormalizer.NormalizeBody(content);
        var imageData = await mediaService.GetImageRenderDataFromContentAsync(normalizedContent);
        var renderedContent = markupParser.ToHtml(normalizedContent, autoParagraph, imageData);
        var post = Post.Create(discussionId, userId, normalizedContent, renderedContent, replyToPostId: replyToPostId, wasNormalized: wasNormalized);

        // Auto-follow discussion if user has the preference enabled
        if (user.AutoFollowOnReply)
        {
            var existingFollow = await followRepository.GetByUserAndDiscussionAsync(userId, discussionId);

            if (existingFollow is null)
            {
                var follow = Follow.CreateForDiscussion(userId, discussionId);
                await followRepository.AddAsync(follow);
                await counterService.IncrementDiscussionFollowerCountAsync(discussionId);
            }
        }

        // Last-reply preview text — computed before the write so the discussion's
        // activity bump + denormalized preview commit in one statement below.
        var plainExcerpt = markupParser.ToPlainText(post.Content);
        if (plainExcerpt.Length > 150) plainExcerpt = plainExcerpt[..150];
        var lastActivityAt = DateTime.UtcNow;

        // Persist atomically — avoids an orphan post if the discussion write fails.
        // RecordReplyAsync is a single targeted UPDATE (LastActivityAt + last-reply
        // preview), replacing the former load + full-entity update and a separate
        // preview write. Counters (PostCount etc.) follow outside the transaction.
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await postRepository.AddAsync(post);
            await discussionRepository.RecordReplyAsync(
                discussion.PublicId, lastActivityAt,
                user.PublicId.Value,
                user.DisplayName,
                user.AvatarFileName,
                user.AvatarThumbnailFileName,
                plainExcerpt);
        });

        // Publish any draft media referenced in the post (only the uploader's own drafts)
        await mediaService.PublishDraftMediaAsync(normalizedContent, userId.Value);

        // Update denormalized counts
        await counterService.IncrementPostCountAsync(discussionId);
        await counterService.IncrementUserReplyCountAsync(userId);

        // Dispatch domain events
        await eventDispatcher.DispatchAsync(post.DomainEvents);

        post.ClearDomainEvents();

        // Send realtime notification
        await realtimeNotifier.NotifyPostCreatedAsync(post, user, discussion);

        var replierAvatarUrl = AvatarHelper.GetAvatarMicroUrl(
            user.PublicId.Value, AvatarEntityType.User,
            user.AvatarRevision, user.AvatarFileName, user.AvatarMicroFileName);
        await realtimeNotifier.NotifyPostCountUpdatedAsync(
            discussion.PublicId, discussion.SpaceId, space?.HubId, 1,
            lastPostExcerpt: plainExcerpt,
            lastReplierId: user.PublicId.Value,
            lastReplierName: user.DisplayName,
            lastReplierAvatarUrl: replierAvatarUrl,
            lastActivityAtUnix: DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Link any uploaded media to this post
        await mediaService.LinkMediaToPostAsync(post.PublicId.Value, normalizedContent);

        return Result<Post>.Success(post);
    }

    public async Task<Result<Post>> UpdatePostAsync(
        PostId postId,
        UserId userId,
        string newContent)
    {
        var post = await postRepository.GetByPublicIdAsync(postId);

        if (post is null)
            return Result<Post>.Failure($"Post '{postId}' not found");

        // Fetch user and discussion for realtime notification
        var user = await userRepository.GetByPublicIdAsync(userId);

        if (user is null)
            return Result<Post>.Failure($"User '{userId}' not found");

        var discussion = await discussionRepository.GetByPublicIdAsync(post.DiscussionId);

        if (discussion is null)
            return Result<Post>.Failure($"Discussion '{post.DiscussionId}' not found");

        try
        {
            var space = await spaceRepository.GetByPublicIdAsync(discussion.SpaceId);
            var autoParagraph = space?.AutoParagraphEnabled ?? true;
            var (normalizedContent, wasNormalized) = contentNormalizer.NormalizeBody(newContent);
            var imageData = await mediaService.GetImageRenderDataFromContentAsync(normalizedContent);
            var renderedContent = markupParser.ToHtml(normalizedContent, autoParagraph, imageData);
            post.UpdateContent(normalizedContent, renderedContent, userId, wasNormalized);
            await postRepository.UpdateAsync(post);

            // Save only new/unsaved revisions
            foreach (var revision in post.UnsavedRevisions)
            {
                await postRepository.AddRevisionAsync(revision);
            }

            post.ClearUnsavedRevisions();

            // Dispatch domain events
            await eventDispatcher.DispatchAsync(post.DomainEvents);
            post.ClearDomainEvents();

            // Send realtime notification
            await realtimeNotifier.NotifyPostEditedAsync(post, user, discussion);

            // Re-link media based on updated content
            await mediaService.LinkMediaToPostAsync(post.PublicId.Value, normalizedContent);

            return Result<Post>.Success(post);
        }
        catch (InvalidOperationException ex)
        {
            return Result<Post>.Failure(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Result<Post>.Failure(ex.Message);
        }
    }

    public async Task<Result<Post>> GetPostAsync(PostId postId)
    {
        var post = await postRepository.GetByPublicIdAsync(postId);

        if (post is null)
            return Result<Post>.Failure($"Post '{postId}' not found");

        return Result<Post>.Success(post);
    }

    public async Task<IEnumerable<Post>> GetPostsByPublicIdsAsync(IEnumerable<PostId> postIds) =>
        await postRepository.GetByPublicIdsAsync(postIds);

    public async Task<PagedResult<Post>> GetPostsByDiscussionAsync(
        DiscussionId discussionId,
        int offset = 0,
        int pageSize = 20) =>
        await postRepository.GetPagedByDiscussionIdAsync(discussionId, offset, pageSize);

    public async Task<Result> DeletePostAsync(PostId postId, UserId userId)
    {
        var post = await postRepository.GetByPublicIdAsync(postId);

        if (post is null)
            return Result.Failure($"Post '{postId}' not found");

        if (!post.CanDelete(userId))
            return Result.Failure("You can only delete your own posts");

        try
        {
            var discussionId = post.DiscussionId;
            var isHardDelete = post.CanHardDelete();

            if (isHardDelete)
            {
                // Hard delete - remove completely from database
                post.HardDelete(userId);
                await eventDispatcher.DispatchAsync(post.DomainEvents);
                await postRepository.DeleteAsync(post);
            }
            else
            {
                // Soft delete - mark as deleted
                post.SoftDelete(userId);
                await postRepository.UpdateAsync(post);
                await eventDispatcher.DispatchAsync(post.DomainEvents);
                post.ClearDomainEvents();
            }

            // Decrement denormalized counts
            await counterService.DecrementPostCountAsync(discussionId);

            if (!post.IsFirstPost)
            {
                if (post.CreatedByUserId is not null)
                    await counterService.DecrementUserReplyCountAsync(post.CreatedByUserId);
                await discussionRepository.RecalculateLastPostAsync(discussionId);
            }

            // Send realtime notification
            await realtimeNotifier.NotifyPostDeletedAsync(postId, discussionId, isHardDelete);

            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<IEnumerable<PostRevision>> GetPostHistoryAsync(PostId postId) =>
        await postRepository.GetRevisionsAsync(postId);

    /// <summary>
    /// Gets posts with enriched data (authors, reactions, reply-to snippets)
    /// </summary>
    public async Task<Result<EnrichedPostsResult>> GetEnrichedPostsByDiscussionAsync(
        DiscussionId discussionId,
        UserId? currentUserId,
        int offset,
        int pageSize)
    {
        // 0. Validate discussion exists
        var discussion = await discussionRepository.GetByPublicIdAsync(discussionId);

        if (discussion is null)
            return Result<EnrichedPostsResult>.Failure($"Discussion '{discussionId}' not found");

        // 1. Fetch posts
        var postsResult = await GetPostsByDiscussionAsync(discussionId, offset, pageSize);
        var visiblePosts = postsResult.Items
            .Where(p => !p.IsDeleted)
            .ToList();

        // 2. Prepare batch IDs for parallel fetching
        var authorIds = visiblePosts
            .Select(p => p.CreatedByUserId)
            .Where(id => id is not null)
            .Select(id => id!)
            .Distinct()
            .ToList();

        var replyToIds = visiblePosts
            .Where(p => p.ReplyToPostId is not null)
            .Select(p => p.ReplyToPostId!)
            .Distinct()
            .ToList();

        var postIds = visiblePosts
            .Select(p => p.PublicId)
            .ToList();

        // 3. Fetch authors, reply-to posts, reactions, and user reactions sequentially
        //    (EF Core DbContext does not support concurrent operations)
        var authors_raw = await userRepository.GetPostAuthorSlimByPublicIdsAsync(authorIds);
        var replyToPosts = replyToIds.Count > 0
            ? await GetPostsByPublicIdsAsync(replyToIds)
            : Enumerable.Empty<Post>();
        var reactionData = await reactionUseCase.GetReactionDataAsync(postIds, currentUserId);

        // 4. Build authors dictionary
        var authorsDict = authors_raw.ToDictionary(u => u.PublicId);
        var authors = new Dictionary<string, AuthorInfo>();

        foreach (var authorId in authorIds)
        {
            if (authorsDict.TryGetValue(authorId.Value, out var user))
            {
                authors[authorId.Value] = new AuthorInfo(
                    user.DisplayName ?? "",
                    null,
                    user.AvatarFileName,
                    user.AvatarThumbnailFileName,
                    user.AvatarMicroFileName,
                    user.AvatarRevision,
                    false,
                    user.CreatedAt,
                    user.DiscussionCount,
                    user.ReplyCount,
                    user.Slug);
            }
            else
                authors[authorId.Value] = new AuthorInfo("Deleted User", null, null, null, null, 0, true, DateTime.MinValue, 0, 0);
        }

        // 5. Build reply-to dictionary (need reply authors — batch from already-fetched authors where possible)
        var replyToPostsDict = new Dictionary<string, ReplyToInfo>();

        if (replyToIds.Count > 0)
        {
            var replyPostsList = replyToPosts
                .Where(p => !p.IsDeleted)
                .ToList();

            // Fetch reply authors not already in authorsDict
            var missingReplyAuthorIds = replyPostsList
                .Select(p => p.CreatedByUserId)
                .Where(id => id is not null && !authorsDict.ContainsKey(id.Value))
                .Select(id => id!)
                .Distinct()
                .ToList();

            if (missingReplyAuthorIds.Count > 0)
            {
                var extraAuthors = await userRepository.GetPostAuthorSlimByPublicIdsAsync(missingReplyAuthorIds);
                foreach (var a in extraAuthors)
                    authorsDict[a.PublicId] = a;
            }

            foreach (var replyPost in replyPostsList)
            {
                var authorName = replyPost.CreatedByUserId is not null && authorsDict.TryGetValue(replyPost.CreatedByUserId.Value, out var replyAuthor)
                    ? replyAuthor.DisplayName ?? ""
                    : "Deleted User";

                var snippet = replyPost.Content.Length > 100
                    ? replyPost.Content.Substring(0, 100) + "..."
                    : replyPost.Content;

                replyToPostsDict[replyPost.PublicId.Value] = new ReplyToInfo(authorName, snippet);
            }
        }

        var reactionCounts = reactionData.Counts;
        var userReactions = reactionData.UserReactions;

        // 6. Build enriched result
        var enrichedPosts = visiblePosts
            .Select((p, index) => new EnrichedPost(
                Post: p,
                PostNumber: offset + index + 1,
                Author: p.CreatedByUserId is not null ? authors[p.CreatedByUserId.Value] : new AuthorInfo("Deleted User", null, null, null, null, 0, true, DateTime.MinValue, 0, 0),
                ReplyTo: p.ReplyToPostId is not null && replyToPostsDict.ContainsKey(p.ReplyToPostId.Value)
                    ? replyToPostsDict[p.ReplyToPostId.Value]
                    : null,
                ReactionCounts: reactionCounts.GetValueOrDefault(p.PublicId.Value, new Dictionary<ReactionType, int>()),
                UserReactions: userReactions.GetValueOrDefault(p.PublicId.Value, [])))
            .ToList();

        return Result<EnrichedPostsResult>.Success(new EnrichedPostsResult(
            enrichedPosts,
            postsResult.Offset,
            postsResult.PageSize,
            postsResult.HasMoreItems));
    }
}

// Supporting types
public record EnrichedPostsResult(
    List<EnrichedPost> Posts,
    int Offset,
    int PageSize,
    bool HasMoreItems);

public record EnrichedPost(
    Post Post,
    int PostNumber,
    AuthorInfo Author,
    ReplyToInfo? ReplyTo,
    Dictionary<ReactionType, int> ReactionCounts,
    List<ReactionType> UserReactions);

public record AuthorInfo(
    string DisplayName,
    string? Role,
    string? AvatarFileName,
    string? AvatarThumbnailFileName,
    string? AvatarMicroFileName,
    int AvatarRevision,
    bool IsDeleted,
    DateTime JoinedAt,
    int DiscussionCount,
    int ReplyCount,
    string? Slug = null);

public record ReplyToInfo(
    string AuthorName,
    string ContentSnippet);
