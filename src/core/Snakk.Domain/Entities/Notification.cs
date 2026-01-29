namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;
using Snakk.Domain.Events;

public class Notification
{
    public NotificationId PublicId { get; private set; }
    public UserId RecipientUserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; }
    public string? Body { get; private set; }

    // Reference to the source entity (post, discussion, etc.)
    public PostId? SourcePostId { get; private set; }
    public DiscussionId? SourceDiscussionId { get; private set; }
    public SpaceId? SourceSpaceId { get; private set; }
    public UserId? ActorUserId { get; private set; }

    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

#pragma warning disable CS8618
    private Notification()
    {
        _domainEvents = [];
    }
#pragma warning restore CS8618

    private Notification(
        NotificationId publicId,
        UserId recipientUserId,
        NotificationType type,
        string title,
        string? body,
        PostId? sourcePostId,
        DiscussionId? sourceDiscussionId,
        SpaceId? sourceSpaceId,
        UserId? actorUserId,
        bool isRead,
        DateTime createdAt,
        DateTime? readAt)
    {
        PublicId = publicId;
        RecipientUserId = recipientUserId;
        Type = type;
        Title = title;
        Body = body;
        SourcePostId = sourcePostId;
        SourceDiscussionId = sourceDiscussionId;
        SourceSpaceId = sourceSpaceId;
        ActorUserId = actorUserId;
        IsRead = isRead;
        CreatedAt = createdAt;
        ReadAt = readAt;
        _domainEvents = [];
    }

    public static Notification CreateForMention(
        UserId recipientUserId,
        UserId mentionerUserId,
        PostId postId,
        DiscussionId discussionId,
        string mentionerDisplayName,
        string discussionTitle)
    {
        var notification = new Notification(
            NotificationId.New(),
            recipientUserId,
            NotificationType.Mention,
            $"{mentionerDisplayName} mentioned you",
            $"In: {discussionTitle}",
            postId,
            discussionId,
            sourceSpaceId: null,
            mentionerUserId,
            isRead: false,
            DateTime.UtcNow,
            readAt: null);

        notification.AddDomainEvent(new NotificationCreatedEvent(
            notification.PublicId, recipientUserId, notification.Type));
        return notification;
    }

    public static Notification CreateForReply(
        UserId recipientUserId,
        UserId replierUserId,
        PostId postId,
        DiscussionId discussionId,
        string replierDisplayName,
        string discussionTitle)
    {
        var notification = new Notification(
            NotificationId.New(),
            recipientUserId,
            NotificationType.Reply,
            $"{replierDisplayName} replied to you",
            $"In: {discussionTitle}",
            postId,
            discussionId,
            sourceSpaceId: null,
            replierUserId,
            isRead: false,
            DateTime.UtcNow,
            readAt: null);

        notification.AddDomainEvent(new NotificationCreatedEvent(
            notification.PublicId, recipientUserId, notification.Type));
        return notification;
    }

    public static Notification CreateForNewPost(
        UserId recipientUserId,
        UserId posterUserId,
        PostId postId,
        DiscussionId discussionId,
        string posterDisplayName,
        string discussionTitle)
    {
        var notification = new Notification(
            NotificationId.New(),
            recipientUserId,
            NotificationType.NewPostInFollowedDiscussion,
            $"{posterDisplayName} posted",
            $"In: {discussionTitle}",
            postId,
            discussionId,
            sourceSpaceId: null,
            posterUserId,
            isRead: false,
            DateTime.UtcNow,
            readAt: null);

        notification.AddDomainEvent(new NotificationCreatedEvent(
            notification.PublicId, recipientUserId, notification.Type));
        return notification;
    }

    public static Notification CreateForNewDiscussion(
        UserId recipientUserId,
        UserId authorUserId,
        DiscussionId discussionId,
        SpaceId spaceId,
        string authorDisplayName,
        string discussionTitle,
        string spaceName)
    {
        var notification = new Notification(
            NotificationId.New(),
            recipientUserId,
            NotificationType.NewDiscussionInFollowedSpace,
            $"{authorDisplayName} started a discussion",
            $"{discussionTitle} in {spaceName}",
            sourcePostId: null,
            discussionId,
            spaceId,
            authorUserId,
            isRead: false,
            DateTime.UtcNow,
            readAt: null);

        notification.AddDomainEvent(new NotificationCreatedEvent(
            notification.PublicId, recipientUserId, notification.Type));
        return notification;
    }

    public static Notification Rehydrate(
        NotificationId publicId,
        UserId recipientUserId,
        NotificationType type,
        string title,
        string? body,
        PostId? sourcePostId,
        DiscussionId? sourceDiscussionId,
        SpaceId? sourceSpaceId,
        UserId? actorUserId,
        bool isRead,
        DateTime createdAt,
        DateTime? readAt)
    {
        return new Notification(publicId, recipientUserId, type, title, body,
            sourcePostId, sourceDiscussionId, sourceSpaceId, actorUserId,
            isRead, createdAt, readAt);
    }

    public void MarkAsRead()
    {
        if (!IsRead)
        {
            IsRead = true;
            ReadAt = DateTime.UtcNow;
        }
    }

    private void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
