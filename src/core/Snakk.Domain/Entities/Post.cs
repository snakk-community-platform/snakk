namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;
using Snakk.Domain.Events;

public class Post
{
    public PostId PublicId { get; private set; }
    public DiscussionId DiscussionId { get; private set; }
    public UserId? CreatedByUserId { get; private set; }
    public string Content { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastModifiedAt { get; private set; }
    public DateTime? EditedAt { get; private set; }
    public bool IsFirstPost { get; private set; }
    public PostId? ReplyToPostId { get; private set; }
    public bool IsDeleted { get; private set; }
    public string RenderedContent { get; private set; }
    public bool HasCodeBlock { get; private set; }
    public bool IsUsersFirstPostInDiscussion { get; private set; }
    public bool IsUsersFirstPostInSpace { get; private set; }
    public bool IsOp { get; private set; }
    public bool IsNecro { get; private set; }
    public bool IsMilestone { get; private set; }
    public int RevisionCount { get; private set; }
    public bool WasNormalized { get; private set; }

    private readonly List<PostRevision> _revisions = [];
    public IReadOnlyCollection<PostRevision> Revisions => _revisions.AsReadOnly();

    private readonly List<PostRevision> _unsavedRevisions = [];
    public IReadOnlyCollection<PostRevision> UnsavedRevisions => _unsavedRevisions.AsReadOnly();

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private Post()
    {
        _revisions = [];
        _unsavedRevisions = [];
        _domainEvents = [];
    }
#pragma warning restore CS8618

    private Post(
        PostId publicId,
        DiscussionId discussionId,
        UserId? createdByUserId,
        string content,
        string renderedContent,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        DateTime? editedAt = null,
        bool isFirstPost = false,
        PostId? replyToPostId = null,
        bool isDeleted = false,
        bool hasCodeBlock = false,
        bool isUsersFirstPostInDiscussion = false,
        bool isUsersFirstPostInSpace = false,
        bool isOp = false,
        bool isNecro = false,
        bool isMilestone = false,
        int revisionCount = 0,
        List<PostRevision>? revisions = null,
        bool wasNormalized = false)
    {
        PublicId = publicId;
        DiscussionId = discussionId;
        CreatedByUserId = createdByUserId;
        Content = content;
        RenderedContent = renderedContent;
        CreatedAt = createdAt;
        LastModifiedAt = lastModifiedAt;
        EditedAt = editedAt;
        IsFirstPost = isFirstPost;
        ReplyToPostId = replyToPostId;
        IsDeleted = isDeleted;
        HasCodeBlock = hasCodeBlock;
        IsUsersFirstPostInDiscussion = isUsersFirstPostInDiscussion;
        IsUsersFirstPostInSpace = isUsersFirstPostInSpace;
        IsOp = isOp;
        IsNecro = isNecro;
        IsMilestone = isMilestone;
        RevisionCount = revisionCount;
        WasNormalized = wasNormalized;
        _revisions = revisions ?? [];
        _unsavedRevisions = [];
        _domainEvents = [];
    }

    public static Post Create(
        DiscussionId discussionId,
        UserId createdByUserId,
        string content,
        string renderedContent,
        bool isFirstPost = false,
        PostId? replyToPostId = null,
        bool wasNormalized = false)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Post content cannot be empty", nameof(content));

        var post = new Post(
            PostId.New(),
            discussionId,
            createdByUserId,
            content,
            renderedContent,
            DateTime.UtcNow,
            isFirstPost: isFirstPost,
            replyToPostId: replyToPostId,
            hasCodeBlock: content.Contains('`'),
            wasNormalized: wasNormalized);

        post.AddDomainEvent(new PostCreatedEvent(post.PublicId, discussionId, createdByUserId));

        return post;
    }

    public static Post Rehydrate(
        PostId publicId,
        DiscussionId discussionId,
        UserId? createdByUserId,
        string content,
        string renderedContent,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        DateTime? editedAt = null,
        bool isFirstPost = false,
        PostId? replyToPostId = null,
        bool isDeleted = false,
        bool hasCodeBlock = false,
        bool isUsersFirstPostInDiscussion = false,
        bool isUsersFirstPostInSpace = false,
        bool isOp = false,
        bool isNecro = false,
        bool isMilestone = false,
        int revisionCount = 0,
        List<PostRevision>? revisions = null,
        bool wasNormalized = false) =>
        new Post(
            publicId,
            discussionId,
            createdByUserId,
            content,
            renderedContent,
            createdAt,
            lastModifiedAt,
            editedAt,
            isFirstPost,
            replyToPostId,
            isDeleted,
            hasCodeBlock,
            isUsersFirstPostInDiscussion,
            isUsersFirstPostInSpace,
            isOp,
            isNecro,
            isMilestone,
            revisionCount,
            revisions,
            wasNormalized);

    public void UpdateContent(string content, string renderedContent, UserId editorUserId, bool wasNormalized = false)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot edit a deleted post");

        if (!CanEdit(editorUserId))
            throw new InvalidOperationException("Only the author can edit this post");

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Post content cannot be empty", nameof(content));

        // Create revision with old content before updating
        var revision = PostRevision.Create(PublicId, Content, editorUserId, RevisionCount + 1);
        _revisions.Add(revision);
        _unsavedRevisions.Add(revision); // Track for persistence
        RevisionCount++;

        Content = content;
        RenderedContent = renderedContent;
        HasCodeBlock = content.Contains('`');
        WasNormalized = wasNormalized;
        EditedAt = DateTime.UtcNow;
        LastModifiedAt = DateTime.UtcNow;

        AddDomainEvent(new PostEditedEvent(PublicId, DiscussionId));
    }

    public void SoftDelete(UserId deletedByUserId)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Post is already deleted");

        IsDeleted = true;
        LastModifiedAt = DateTime.UtcNow;

        AddDomainEvent(new PostDeletedEvent(PublicId, DiscussionId, deletedByUserId, false));
    }

    public void HardDelete(UserId deletedByUserId)
    {
        AddDomainEvent(new PostDeletedEvent(PublicId, DiscussionId, deletedByUserId, true));
    }

    public bool CanHardDelete() =>
        CreatedAt > DateTime.UtcNow.AddMinutes(-5);

    public bool CanEdit(UserId userId) =>
        CreatedByUserId?.Value == userId.Value;

    public bool CanDelete(UserId userId) =>
        CreatedByUserId?.Value == userId.Value;

    private void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public void ClearUnsavedRevisions()
    {
        _unsavedRevisions.Clear();
    }
}
