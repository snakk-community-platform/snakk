namespace Snakk.Application.UseCases;

using Snakk.Domain;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;
using Snakk.Application.Services;

public class PostUseCase(
    IPostRepository postRepository,
    IDiscussionRepository discussionRepository,
    IUserRepository userRepository,
    IDomainEventDispatcher eventDispatcher,
    IRealtimeNotifier realtimeNotifier,
    ICounterService counterService) : UseCaseBase
{
    private readonly IPostRepository _postRepository = postRepository;
    private readonly IDiscussionRepository _discussionRepository = discussionRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IDomainEventDispatcher _eventDispatcher = eventDispatcher;
    private readonly IRealtimeNotifier _realtimeNotifier = realtimeNotifier;
    private readonly ICounterService _counterService = counterService;

    public async Task<Result<Post>> CreatePostAsync(
        DiscussionId discussionId,
        UserId userId,
        string content,
        PostId? replyToPostId = null)
    {
        // Validate discussion exists
        var discussion = await _discussionRepository.GetByPublicIdAsync(discussionId);
        if (discussion == null)
            return Result<Post>.Failure($"Discussion '{discussionId}' not found");

        // Check if discussion is locked
        if (discussion.IsLocked)
            return Result<Post>.Failure("Cannot post to a locked discussion");

        // Validate user exists
        var user = await _userRepository.GetByPublicIdAsync(userId);
        if (user == null)
            return Result<Post>.Failure($"User '{userId}' not found");

        // If replying, validate reply-to post exists
        if (replyToPostId != null)
        {
            var replyToPost = await _postRepository.GetByPublicIdAsync(replyToPostId);
            if (replyToPost == null)
                return Result<Post>.Failure($"Reply-to post '{replyToPostId}' not found");
        }

        // Create post
        var post = Post.Create(discussionId, userId, content, replyToPostId: replyToPostId);

        // Update discussion activity
        discussion.UpdateActivity();

        // Persist
        await _postRepository.AddAsync(post);
        await _discussionRepository.UpdateAsync(discussion);

        // Update denormalized counts
        await _counterService.IncrementPostCountAsync(discussionId);

        // Dispatch domain events
        await _eventDispatcher.DispatchAsync(post.DomainEvents);

        post.ClearDomainEvents();

        // Send realtime notification
        await _realtimeNotifier.NotifyPostCreatedAsync(post, user, discussion);

        return Result<Post>.Success(post);
    }

    public async Task<Result<Post>> UpdatePostAsync(
        PostId postId,
        UserId userId,
        string newContent)
    {
        var post = await _postRepository.GetByPublicIdAsync(postId);
        if (post == null)
            return Result<Post>.Failure($"Post '{postId}' not found");

        // Fetch user and discussion for realtime notification
        var user = await _userRepository.GetByPublicIdAsync(userId);
        if (user == null)
            return Result<Post>.Failure($"User '{userId}' not found");

        var discussion = await _discussionRepository.GetByPublicIdAsync(post.DiscussionId);
        if (discussion == null)
            return Result<Post>.Failure($"Discussion '{post.DiscussionId}' not found");

        try
        {
            post.UpdateContent(newContent, userId);
            await _postRepository.UpdateAsync(post);

            // Save revisions
            foreach (var revision in post.Revisions)
            {
                await _postRepository.AddRevisionAsync(revision);
            }

            // Dispatch domain events
            await _eventDispatcher.DispatchAsync(post.DomainEvents);
            post.ClearDomainEvents();

            // Send realtime notification
            await _realtimeNotifier.NotifyPostEditedAsync(post, user, discussion);

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
        var post = await _postRepository.GetByPublicIdAsync(postId);
        if (post == null)
            return Result<Post>.Failure($"Post '{postId}' not found");

        return Result<Post>.Success(post);
    }

    public async Task<IEnumerable<Post>> GetPostsByPublicIdsAsync(IEnumerable<PostId> postIds)
    {
        return await _postRepository.GetByPublicIdsAsync(postIds);
    }

    public async Task<PagedResult<Post>> GetPostsByDiscussionAsync(
        DiscussionId discussionId,
        int offset = 0,
        int pageSize = 20)
    {
        return await _postRepository.GetPagedByDiscussionIdAsync(discussionId, offset, pageSize);
    }

    public async Task<Result> DeletePostAsync(PostId postId, UserId userId)
    {
        var post = await _postRepository.GetByPublicIdAsync(postId);
        if (post == null)
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
                await _eventDispatcher.DispatchAsync(post.DomainEvents);
                await _postRepository.DeleteAsync(post);
            }
            else
            {
                // Soft delete - mark as deleted
                post.SoftDelete(userId);
                await _postRepository.UpdateAsync(post);
                await _eventDispatcher.DispatchAsync(post.DomainEvents);
                post.ClearDomainEvents();
            }

            // Decrement denormalized counts
            await _counterService.DecrementPostCountAsync(discussionId);

            // Send realtime notification
            await _realtimeNotifier.NotifyPostDeletedAsync(postId, discussionId, isHardDelete);

            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<IEnumerable<PostRevision>> GetPostHistoryAsync(PostId postId)
    {
        return await _postRepository.GetRevisionsAsync(postId);
    }
}
