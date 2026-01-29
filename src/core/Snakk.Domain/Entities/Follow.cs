namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;
using Snakk.Domain.Events;

public class Follow
{
    public FollowId PublicId { get; private set; }
    public UserId UserId { get; private set; }
    public FollowTargetType TargetType { get; private set; }
    public DiscussionId? DiscussionId { get; private set; }
    public SpaceId? SpaceId { get; private set; }
    public UserId? FollowedUserId { get; private set; }
    /// <summary>
    /// The notification level for this follow.
    /// Only meaningful for Space follows; Discussion follows always include posts.
    /// </summary>
    public FollowLevel Level { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

#pragma warning disable CS8618
    private Follow()
    {
        _domainEvents = [];
    }
#pragma warning restore CS8618

    private Follow(
        FollowId publicId,
        UserId userId,
        FollowTargetType targetType,
        DiscussionId? discussionId,
        SpaceId? spaceId,
        UserId? followedUserId,
        FollowLevel level,
        DateTime createdAt)
    {
        PublicId = publicId;
        UserId = userId;
        TargetType = targetType;
        DiscussionId = discussionId;
        SpaceId = spaceId;
        FollowedUserId = followedUserId;
        Level = level;
        CreatedAt = createdAt;
        _domainEvents = [];
    }

    public static Follow CreateForDiscussion(UserId userId, DiscussionId discussionId)
    {
        var follow = new Follow(
            FollowId.New(),
            userId,
            FollowTargetType.Discussion,
            discussionId,
            spaceId: null,
            followedUserId: null,
            FollowLevel.DiscussionsAndPosts, // Always includes posts for discussions
            DateTime.UtcNow);

        follow.AddDomainEvent(new FollowCreatedEvent(
            follow.PublicId, userId, FollowTargetType.Discussion, discussionId.Value));
        return follow;
    }

    public static Follow CreateForSpace(UserId userId, SpaceId spaceId, FollowLevel level = FollowLevel.DiscussionsOnly)
    {
        var follow = new Follow(
            FollowId.New(),
            userId,
            FollowTargetType.Space,
            discussionId: null,
            spaceId,
            followedUserId: null,
            level,
            DateTime.UtcNow);

        follow.AddDomainEvent(new FollowCreatedEvent(
            follow.PublicId, userId, FollowTargetType.Space, spaceId.Value));
        return follow;
    }

    public static Follow CreateForUser(UserId userId, UserId followedUserId)
    {
        var follow = new Follow(
            FollowId.New(),
            userId,
            FollowTargetType.User,
            discussionId: null,
            spaceId: null,
            followedUserId,
            FollowLevel.DiscussionsAndPosts, // Users get all activity
            DateTime.UtcNow);

        follow.AddDomainEvent(new FollowCreatedEvent(
            follow.PublicId, userId, FollowTargetType.User, followedUserId.Value));
        return follow;
    }

    public static Follow Rehydrate(
        FollowId publicId,
        UserId userId,
        FollowTargetType targetType,
        DiscussionId? discussionId,
        SpaceId? spaceId,
        UserId? followedUserId,
        FollowLevel level,
        DateTime createdAt)
    {
        return new Follow(publicId, userId, targetType, discussionId, spaceId, followedUserId, level, createdAt);
    }

    /// <summary>
    /// Updates the notification level for this follow.
    /// Only meaningful for Space follows.
    /// </summary>
    public void UpdateLevel(FollowLevel level)
    {
        Level = level;
    }

    public void MarkForRemoval()
    {
        var targetId = TargetType switch
        {
            FollowTargetType.Discussion => DiscussionId?.Value ?? "",
            FollowTargetType.Space => SpaceId?.Value ?? "",
            FollowTargetType.User => FollowedUserId?.Value ?? "",
            _ => ""
        };
        AddDomainEvent(new FollowRemovedEvent(PublicId, UserId, TargetType, targetId));
    }

    private void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
