namespace Snakk.Application.UseCases;

using Snakk.Domain;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;
using Snakk.Application.Repositories;
using Snakk.Application.Services;

public class DiscussionUseCase(
    IDiscussionRepository discussionRepository,
    ISpaceRepository spaceRepository,
    IUserRepository userRepository,
    IPostRepository postRepository,
    IDomainEventDispatcher eventDispatcher,
    ICounterService counterService,
    IMarkupParser markupParser,
    IContentNormalizer contentNormalizer,
    IRealtimeNotifier realtimeNotifier,
    IMediaService mediaService,
    IModerationRepository moderationRepository,
    IUnitOfWork unitOfWork) : UseCaseBase
{
    public async Task<Result<Discussion>> CreateDiscussionAsync(
        SpaceId spaceId,
        UserId userId,
        string title,
        string slug,
        string firstPostContent,
        DiscussionTypeEnum type = DiscussionTypeEnum.Standard,
        bool isAdult = false)
    {
        // Validate space exists
        var space = await spaceRepository.GetByPublicIdAsync(spaceId);

        if (space is null)
            return Result<Discussion>.Failure($"Space '{spaceId}' not found");

        // Validate user exists
        var user = await userRepository.GetByPublicIdAsync(userId);

        if (user is null)
            return Result<Discussion>.Failure($"User '{userId}' not found");

        // Check if user is banned
        var isBanned = await moderationRepository.IsUserBannedAsync(userId.Value, spacePublicId: spaceId.Value);

        if (isBanned)
            return Result<Discussion>.Failure("You are currently banned from posting in this space");

        // Adult-only spaces force the flag; mixed-mode spaces honour the caller's choice; standard spaces never adult.
        var effectiveIsAdult = space.IsAdultOnly || (space.AllowsAdultContent && isAdult);

        // Normalize title and first post body
        var (normalizedTitle, titleNormalized) = contentNormalizer.NormalizeTitle(title);
        var (normalizedFirstPost, bodyNormalized) = contentNormalizer.NormalizeBody(firstPostContent);

        // Create discussion
        var discussion = Discussion.Create(spaceId, userId, normalizedTitle, slug, type, effectiveIsAdult, wasNormalized: titleNormalized || bodyNormalized);

        // Create first post
        var renderedFirstPost = markupParser.ToHtml(normalizedFirstPost, space.AutoParagraphEnabled);
        var firstPost = Post.Create(discussion.PublicId, userId, normalizedFirstPost, renderedFirstPost, isFirstPost: true, wasNormalized: bodyNormalized);

        // Persist atomically — without a transaction an orphan Discussion row would exist if AddAsync(firstPost) fails
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await discussionRepository.AddAsync(discussion);
            await postRepository.AddAsync(firstPost);
        });

        // Update denormalized counts
        await counterService.IncrementDiscussionCountAsync(spaceId);
        await counterService.IncrementPostCountAsync(discussion.PublicId);
        await counterService.IncrementUserDiscussionCountAsync(userId);

        // Dispatch domain events
        await eventDispatcher.DispatchAsync(discussion.DomainEvents);
        await eventDispatcher.DispatchAsync(firstPost.DomainEvents);

        discussion.ClearDomainEvents();
        firstPost.ClearDomainEvents();

        // Publish any draft media referenced in the first post
        await mediaService.PublishDraftMediaAsync(normalizedFirstPost, userId.Value);

        // Notify space, hub, and global subscribers about the new discussion
        await realtimeNotifier.NotifyDiscussionCreatedAsync(discussion.PublicId, spaceId, space.HubId);

        return Result<Discussion>.Success(discussion);
    }

    public async Task<Result<Discussion>> GetDiscussionAsync(DiscussionId discussionId)
    {
        var discussion = await discussionRepository.GetByPublicIdAsync(discussionId);

        if (discussion is null)
            return Result<Discussion>.Failure($"Discussion '{discussionId}' not found");

        return Result<Discussion>.Success(discussion);
    }

    public async Task<Result<Discussion>> GetDiscussionIncludingDeletedAsync(DiscussionId discussionId)
    {
        var discussion = await discussionRepository.GetByPublicIdIncludingDeletedAsync(discussionId);

        if (discussion is null)
            return Result<Discussion>.Failure($"Discussion '{discussionId}' not found");

        return Result<Discussion>.Success(discussion);
    }

    public async Task<IReadOnlyList<Domain.Repositories.DiscussionSummary>> GetDiscussionsByIdsAsync(
        IEnumerable<DiscussionId> ids) =>
        await discussionRepository.GetSummariesByPublicIdsAsync(ids);

    public async Task<PagedResult<Discussion>> GetDiscussionsBySpaceAsync(SpaceId spaceId, int offset = 0, int pageSize = 20) =>
        await discussionRepository.GetBySpaceIdAsync(spaceId, offset, pageSize);

    public async Task<Result<Discussion>> UpdateDiscussionTitleAsync(
        DiscussionId discussionId,
        string newTitle)
    {
        var discussion = await discussionRepository.GetByPublicIdAsync(discussionId);

        if (discussion is null)
            return Result<Discussion>.Failure($"Discussion '{discussionId}' not found");

        try
        {
            discussion.UpdateTitle(newTitle);
            await discussionRepository.UpdateAsync(discussion);
            await realtimeNotifier.NotifyDiscussionTitleUpdatedAsync(discussionId, newTitle);

            return Result<Discussion>.Success(discussion);
        }
        catch (InvalidOperationException ex)
        {
            return Result<Discussion>.Failure(ex.Message);
        }
    }

    public async Task<Result> PinDiscussionAsync(DiscussionId discussionId)
    {
        var discussion = await discussionRepository.GetByPublicIdAsync(discussionId);

        if (discussion is null)
            return Result.Failure($"Discussion '{discussionId}' not found");

        discussion.Pin();
        await discussionRepository.UpdateAsync(discussion);
        await realtimeNotifier.NotifyDiscussionPinnedAsync(discussionId, discussion.SpaceId, isPinned: true);

        return Result.Success();
    }

    public async Task<Result> UnpinDiscussionAsync(DiscussionId discussionId)
    {
        var discussion = await discussionRepository.GetByPublicIdAsync(discussionId);

        if (discussion is null)
            return Result.Failure($"Discussion '{discussionId}' not found");

        discussion.Unpin();
        await discussionRepository.UpdateAsync(discussion);
        await realtimeNotifier.NotifyDiscussionPinnedAsync(discussionId, discussion.SpaceId, isPinned: false);

        return Result.Success();
    }

    public async Task<Result> LockDiscussionAsync(DiscussionId discussionId)
    {
        var discussion = await discussionRepository.GetByPublicIdAsync(discussionId);

        if (discussion is null)
            return Result.Failure($"Discussion '{discussionId}' not found");

        discussion.Lock();
        await discussionRepository.UpdateAsync(discussion);
        await realtimeNotifier.NotifyDiscussionLockedAsync(discussionId);

        return Result.Success();
    }

    public async Task<Result> UnlockDiscussionAsync(DiscussionId discussionId)
    {
        var discussion = await discussionRepository.GetByPublicIdAsync(discussionId);

        if (discussion is null)
            return Result.Failure($"Discussion '{discussionId}' not found");

        discussion.Unlock();
        await discussionRepository.UpdateAsync(discussion);
        await realtimeNotifier.NotifyDiscussionUnlockedAsync(discussionId);

        return Result.Success();
    }

    /// <summary>
    /// Gets the sequential post number for a specific post within a discussion
    /// </summary>
    public async Task<Result<int>> GetPostNumberAsync(DiscussionId discussionId, PostId postId)
    {
        // Validate discussion exists
        var discussion = await discussionRepository.GetByPublicIdAsync(discussionId);

        if (discussion is null)
            return Result<int>.Failure("Discussion not found");

        // Get post and validate it belongs to discussion
        var post = await postRepository.GetByPublicIdAsync(postId);

        if (post is null)
            return Result<int>.Failure("Post not found");

        if (post.DiscussionId != discussion.PublicId)
            return Result<int>.Failure("Post does not belong to this discussion");

        // Count posts created before or at this post's timestamp
        var postNumber = await postRepository.GetPostNumberInDiscussionAsync(discussionId, post.CreatedAt);

        return Result<int>.Success(postNumber);
    }

    /// <summary>
    /// Gets the first post content for preview purposes
    /// </summary>
    public async Task<Result<string>> GetFirstPostPreviewAsync(DiscussionId discussionId)
    {
        // Validate discussion exists
        var discussion = await discussionRepository.GetByPublicIdAsync(discussionId);

        if (discussion is null)
            return Result<string>.Failure("Discussion not found");

        // Get first post
        var firstPost = await postRepository.GetFirstPostByDiscussionIdAsync(discussionId);

        if (firstPost is null)
            return Result<string>.Failure("First post not found");

        return Result<string>.Success(firstPost.Content);
    }
}
