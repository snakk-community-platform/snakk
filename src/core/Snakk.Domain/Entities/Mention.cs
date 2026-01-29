namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;
using Snakk.Domain.Events;

public class Mention
{
    public MentionId PublicId { get; private set; }
    public PostId PostId { get; private set; }
    public UserId MentionedUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

#pragma warning disable CS8618
    private Mention()
    {
        _domainEvents = [];
    }
#pragma warning restore CS8618

    private Mention(
        MentionId publicId,
        PostId postId,
        UserId mentionedUserId,
        DateTime createdAt)
    {
        PublicId = publicId;
        PostId = postId;
        MentionedUserId = mentionedUserId;
        CreatedAt = createdAt;
        _domainEvents = [];
    }

    public static Mention Create(PostId postId, UserId mentionedUserId)
    {
        var mention = new Mention(
            MentionId.New(),
            postId,
            mentionedUserId,
            DateTime.UtcNow);

        mention.AddDomainEvent(new MentionCreatedEvent(mention.PublicId, postId, mentionedUserId));
        return mention;
    }

    public static Mention Rehydrate(
        MentionId publicId,
        PostId postId,
        UserId mentionedUserId,
        DateTime createdAt)
    {
        return new Mention(publicId, postId, mentionedUserId, createdAt);
    }

    private void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
