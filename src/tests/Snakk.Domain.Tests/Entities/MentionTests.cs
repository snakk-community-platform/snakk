using Snakk.Domain.Entities;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class MentionTests
{
    #region Create Tests

    [Test]
    public async Task Create_SetsCorrectProperties()
    {
        // Arrange
        var postId = PostId.New();
        var mentionedUserId = UserId.New();

        // Act
        var mention = Mention.Create(postId, mentionedUserId);

        // Assert
        await Assert.That(mention).IsNotNull();
        await Assert.That(mention.PublicId).IsNotNull();
        await Assert.That(mention.PostId).IsEqualTo(postId);
        await Assert.That(mention.MentionedUserId).IsEqualTo(mentionedUserId);
        await Assert.That(mention.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Create_RaisesMentionCreatedEvent()
    {
        // Arrange
        var postId = PostId.New();
        var mentionedUserId = UserId.New();

        // Act
        var mention = Mention.Create(postId, mentionedUserId);

        // Assert
        await Assert.That(mention.DomainEvents).Count().IsEqualTo(1);
        var domainEvent = mention.DomainEvents.First();
        await Assert.That(domainEvent).IsTypeOf<MentionCreatedEvent>();

        var mentionEvent = (MentionCreatedEvent)domainEvent;
        await Assert.That(mentionEvent.MentionId).IsEqualTo(mention.PublicId);
        await Assert.That(mentionEvent.PostId).IsEqualTo(postId);
        await Assert.That(mentionEvent.MentionedUserId).IsEqualTo(mentionedUserId);
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_RestoresAllProperties()
    {
        // Arrange
        var publicId = MentionId.New();
        var postId = PostId.New();
        var mentionedUserId = UserId.New();
        var createdAt = DateTime.UtcNow.AddHours(-3);

        // Act
        var mention = Mention.Rehydrate(publicId, postId, mentionedUserId, createdAt);

        // Assert
        await Assert.That(mention.PublicId).IsEqualTo(publicId);
        await Assert.That(mention.PostId).IsEqualTo(postId);
        await Assert.That(mention.MentionedUserId).IsEqualTo(mentionedUserId);
        await Assert.That(mention.CreatedAt).IsEqualTo(createdAt);
    }

    [Test]
    public async Task Rehydrate_DoesNotRaiseEvents()
    {
        // Act
        var mention = Mention.Rehydrate(MentionId.New(), PostId.New(), UserId.New(), DateTime.UtcNow);

        // Assert
        await Assert.That(mention.DomainEvents).IsEmpty();
    }

    #endregion

    #region ClearDomainEvents Tests

    [Test]
    public async Task ClearDomainEvents_ClearsEvents()
    {
        // Arrange
        var mention = Mention.Create(PostId.New(), UserId.New());
        await Assert.That(mention.DomainEvents).Count().IsEqualTo(1);

        // Act
        mention.ClearDomainEvents();

        // Assert
        await Assert.That(mention.DomainEvents).IsEmpty();
    }

    #endregion
}
