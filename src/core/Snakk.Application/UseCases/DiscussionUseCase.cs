namespace Snakk.Application.UseCases;

using Snakk.Domain;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;
using Snakk.Application.Services;

public class DiscussionUseCase(
    IDiscussionRepository discussionRepository,
    ISpaceRepository spaceRepository,
    IUserRepository userRepository,
    IPostRepository postRepository,
    IDomainEventDispatcher eventDispatcher,
    ICounterService counterService) : UseCaseBase
{
    private readonly IDiscussionRepository _discussionRepository = discussionRepository;
    private readonly ISpaceRepository _spaceRepository = spaceRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPostRepository _postRepository = postRepository;
    private readonly IDomainEventDispatcher _eventDispatcher = eventDispatcher;
    private readonly ICounterService _counterService = counterService;

    public async Task<Result<Discussion>> CreateDiscussionAsync(
        SpaceId spaceId,
        UserId userId,
        string title,
        string slug,
        string firstPostContent)
    {
        // Validate space exists
        var space = await _spaceRepository.GetByPublicIdAsync(spaceId);
        if (space == null)
            return Result<Discussion>.Failure($"Space '{spaceId}' not found");

        // Validate user exists
        var user = await _userRepository.GetByPublicIdAsync(userId);
        if (user == null)
            return Result<Discussion>.Failure($"User '{userId}' not found");

        // Create discussion
        var discussion = Discussion.Create(spaceId, userId, title, slug);

        // Create first post
        var firstPost = Post.Create(discussion.PublicId, userId, firstPostContent, isFirstPost: true);

        // Persist
        await _discussionRepository.AddAsync(discussion);
        await _postRepository.AddAsync(firstPost);

        // Update denormalized counts
        await _counterService.IncrementDiscussionCountAsync(spaceId);
        await _counterService.IncrementPostCountAsync(discussion.PublicId);

        // Dispatch domain events
        await _eventDispatcher.DispatchAsync(discussion.DomainEvents);
        await _eventDispatcher.DispatchAsync(firstPost.DomainEvents);

        discussion.ClearDomainEvents();
        firstPost.ClearDomainEvents();

        return Result<Discussion>.Success(discussion);
    }

    public async Task<Result<Discussion>> GetDiscussionAsync(DiscussionId discussionId)
    {
        var discussion = await _discussionRepository.GetByPublicIdAsync(discussionId);
        if (discussion == null)
            return Result<Discussion>.Failure($"Discussion '{discussionId}' not found");

        return Result<Discussion>.Success(discussion);
    }

    public async Task<PagedResult<Discussion>> GetDiscussionsBySpaceAsync(SpaceId spaceId, int offset = 0, int pageSize = 20)
    {
        return await _discussionRepository.GetBySpaceIdAsync(spaceId, offset, pageSize);
    }

    public async Task<Result<Discussion>> UpdateDiscussionTitleAsync(
        DiscussionId discussionId,
        string newTitle)
    {
        var discussion = await _discussionRepository.GetByPublicIdAsync(discussionId);
        if (discussion == null)
            return Result<Discussion>.Failure($"Discussion '{discussionId}' not found");

        try
        {
            discussion.UpdateTitle(newTitle);
            await _discussionRepository.UpdateAsync(discussion);
            return Result<Discussion>.Success(discussion);
        }
        catch (InvalidOperationException ex)
        {
            return Result<Discussion>.Failure(ex.Message);
        }
    }

    public async Task<Result> PinDiscussionAsync(DiscussionId discussionId)
    {
        var discussion = await _discussionRepository.GetByPublicIdAsync(discussionId);
        if (discussion == null)
            return Result.Failure($"Discussion '{discussionId}' not found");

        discussion.Pin();
        await _discussionRepository.UpdateAsync(discussion);

        return Result.Success();
    }

    public async Task<Result> UnpinDiscussionAsync(DiscussionId discussionId)
    {
        var discussion = await _discussionRepository.GetByPublicIdAsync(discussionId);
        if (discussion == null)
            return Result.Failure($"Discussion '{discussionId}' not found");

        discussion.Unpin();
        await _discussionRepository.UpdateAsync(discussion);

        return Result.Success();
    }

    public async Task<Result> LockDiscussionAsync(DiscussionId discussionId)
    {
        var discussion = await _discussionRepository.GetByPublicIdAsync(discussionId);
        if (discussion == null)
            return Result.Failure($"Discussion '{discussionId}' not found");

        discussion.Lock();
        await _discussionRepository.UpdateAsync(discussion);

        return Result.Success();
    }

    public async Task<Result> UnlockDiscussionAsync(DiscussionId discussionId)
    {
        var discussion = await _discussionRepository.GetByPublicIdAsync(discussionId);
        if (discussion == null)
            return Result.Failure($"Discussion '{discussionId}' not found");

        discussion.Unlock();
        await _discussionRepository.UpdateAsync(discussion);

        return Result.Success();
    }
}
