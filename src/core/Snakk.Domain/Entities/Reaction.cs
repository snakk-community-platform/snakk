namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;
using Snakk.Domain.Events;

public class Reaction
{
    public ReactionId PublicId { get; private set; }
    public PostId PostId { get; private set; }
    public UserId UserId { get; private set; }
    public ReactionType Type { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

#pragma warning disable CS8618
    private Reaction()
    {
        _domainEvents = [];
    }
#pragma warning restore CS8618

    private Reaction(
        ReactionId publicId,
        PostId postId,
        UserId userId,
        ReactionType type,
        DateTime createdAt)
    {
        PublicId = publicId;
        PostId = postId;
        UserId = userId;
        Type = type;
        CreatedAt = createdAt;
        _domainEvents = [];
    }

    public static Reaction Create(PostId postId, UserId userId, ReactionType type)
    {
        var reaction = new Reaction(
            ReactionId.New(),
            postId,
            userId,
            type,
            DateTime.UtcNow);

        reaction.AddDomainEvent(new ReactionAddedEvent(reaction.PublicId, postId, userId, type));
        return reaction;
    }

    public static Reaction Rehydrate(
        ReactionId publicId,
        PostId postId,
        UserId userId,
        ReactionType type,
        DateTime createdAt)
    {
        return new Reaction(publicId, postId, userId, type, createdAt);
    }

    public void MarkForRemoval()
    {
        AddDomainEvent(new ReactionRemovedEvent(PublicId, PostId, UserId, Type));
    }

    private void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
