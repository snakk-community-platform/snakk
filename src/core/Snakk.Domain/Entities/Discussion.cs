namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;
using Snakk.Domain.Events;

public class Discussion
{
    public DiscussionId PublicId { get; private set; }
    public SpaceId SpaceId { get; private set; }
    public UserId CreatedByUserId { get; private set; }
    public string Title { get; private set; }
    public string Slug { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastModifiedAt { get; private set; }
    public DateTime? LastActivityAt { get; private set; }
    public bool IsPinned { get; private set; }
    public bool IsLocked { get; private set; }

    private readonly List<Post> _posts = [];
    public IReadOnlyCollection<Post> Posts => _posts.AsReadOnly();

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private Discussion()
    {
        _posts = [];
        _domainEvents = [];
    }
#pragma warning restore CS8618

    private Discussion(
        DiscussionId publicId,
        SpaceId spaceId,
        UserId createdByUserId,
        string title,
        string slug,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        DateTime? lastActivityAt = null,
        bool isPinned = false,
        bool isLocked = false,
        List<Post>? posts = null)
    {
        PublicId = publicId;
        SpaceId = spaceId;
        CreatedByUserId = createdByUserId;
        Title = title;
        Slug = slug;
        CreatedAt = createdAt;
        LastModifiedAt = lastModifiedAt;
        LastActivityAt = lastActivityAt;
        IsPinned = isPinned;
        IsLocked = isLocked;
        _posts = posts ?? [];
        _domainEvents = [];
    }

    public static Discussion Create(
        SpaceId spaceId,
        UserId createdByUserId,
        string title,
        string slug)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Discussion title cannot be empty", nameof(title));

        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Discussion slug cannot be empty", nameof(slug));

        var discussion = new Discussion(
            DiscussionId.New(),
            spaceId,
            createdByUserId,
            title,
            slug,
            DateTime.UtcNow,
            lastActivityAt: DateTime.UtcNow);

        discussion.AddDomainEvent(new DiscussionCreatedEvent(discussion.PublicId, spaceId, createdByUserId));

        return discussion;
    }

    public static Discussion Rehydrate(
        DiscussionId publicId,
        SpaceId spaceId,
        UserId createdByUserId,
        string title,
        string slug,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        DateTime? lastActivityAt = null,
        bool isPinned = false,
        bool isLocked = false,
        List<Post>? posts = null)
    {
        return new Discussion(
            publicId,
            spaceId,
            createdByUserId,
            title,
            slug,
            createdAt,
            lastModifiedAt,
            lastActivityAt,
            isPinned,
            isLocked,
            posts);
    }

    public static Discussion RehydrateForList(
        DiscussionId publicId,
        SpaceId spaceId,
        UserId createdByUserId,
        string title,
        string slug,
        DateTime createdAt,
        DateTime? lastActivityAt,
        bool isPinned,
        bool isLocked)
    {
        return new Discussion(
            publicId,
            spaceId,
            createdByUserId,
            title,
            slug,
            createdAt,
            lastModifiedAt: null,
            lastActivityAt,
            isPinned,
            isLocked,
            posts: []);
    }

    public void UpdateTitle(string title)
    {
        if (IsLocked)
            throw new InvalidOperationException("Cannot update a locked discussion");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Discussion title cannot be empty", nameof(title));

        Title = title;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Pin()
    {
        IsPinned = true;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Unpin()
    {
        IsPinned = false;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Lock()
    {
        IsLocked = true;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Unlock()
    {
        IsLocked = false;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void UpdateActivity()
    {
        LastActivityAt = DateTime.UtcNow;
    }

    private void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
