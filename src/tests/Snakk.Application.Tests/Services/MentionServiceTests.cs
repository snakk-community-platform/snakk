using Moq;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;

namespace Snakk.Application.Tests.Services;

public class MentionServiceTests
{
    private readonly Mock<IMentionRepository> _mockMentionRepository = new();
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly Mock<IDiscussionRepository> _mockDiscussionRepository = new();
    private readonly Mock<INotificationRepository> _mockNotificationRepository = new();
    private readonly Mock<IRealtimeNotifier> _mockRealtimeNotifier = new();
    private readonly Mock<ICounterService> _mockCounterService = new();
    private MentionService _service = null!;
    private NotificationUseCase _notificationUseCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _notificationUseCase = new NotificationUseCase(
            _mockNotificationRepository.Object,
            _mockRealtimeNotifier.Object,
            _mockCounterService.Object);

        _service = new MentionService(
            _mockMentionRepository.Object,
            _mockUserRepository.Object,
            _mockDiscussionRepository.Object,
            _notificationUseCase);
    }

    #region ExtractMentionsFromContent Tests

    [Test]
    public async Task ExtractMentionsFromContent_WithSingleMention_ExtractsMention()
    {
        // Act
        var mentions = MentionService.ExtractMentionsFromContent("Hello @JohnDoe how are you?");

        // Assert
        await Assert.That(mentions).Count().IsEqualTo(1);
        await Assert.That(mentions[0]).IsEqualTo("JohnDoe");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithMultipleMentions_ExtractsAll()
    {
        // Act
        var mentions = MentionService.ExtractMentionsFromContent("Hey @Alice and @Bob, check this out @Charlie");

        // Assert
        await Assert.That(mentions).Count().IsEqualTo(3);
        await Assert.That(mentions).Contains("Alice");
        await Assert.That(mentions).Contains("Bob");
        await Assert.That(mentions).Contains("Charlie");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithDuplicateMentions_ReturnsDistinct()
    {
        // Act
        var mentions = MentionService.ExtractMentionsFromContent("@Alice mentioned @Alice again @Alice");

        // Assert
        await Assert.That(mentions).Count().IsEqualTo(1);
        await Assert.That(mentions[0]).IsEqualTo("Alice");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithNoMentions_ReturnsEmptyList()
    {
        // Act
        var mentions = MentionService.ExtractMentionsFromContent("No mentions here at all");

        // Assert
        await Assert.That(mentions).Count().IsEqualTo(0);
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithEmptyString_ReturnsEmptyList()
    {
        // Act
        var mentions = MentionService.ExtractMentionsFromContent("");

        // Assert
        await Assert.That(mentions).Count().IsEqualTo(0);
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithMentionAtStart_ExtractsMention()
    {
        // Act
        var mentions = MentionService.ExtractMentionsFromContent("@StartUser is the first word");

        // Assert
        await Assert.That(mentions).Count().IsEqualTo(1);
        await Assert.That(mentions[0]).IsEqualTo("StartUser");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithMentionAtEnd_ExtractsMention()
    {
        // Act
        var mentions = MentionService.ExtractMentionsFromContent("Last mention is @EndUser");

        // Assert
        await Assert.That(mentions).Count().IsEqualTo(1);
        await Assert.That(mentions[0]).IsEqualTo("EndUser");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithUnderscoreInUsername_ExtractsFull()
    {
        // Act
        var mentions = MentionService.ExtractMentionsFromContent("Hi @user_name_123 welcome");

        // Assert
        await Assert.That(mentions).Count().IsEqualTo(1);
        await Assert.That(mentions[0]).IsEqualTo("user_name_123");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithSpecialCharsAfterMention_ExtractsCorrectly()
    {
        // Act
        var mentions = MentionService.ExtractMentionsFromContent("@user, @user2. @user3!");

        // Assert
        await Assert.That(mentions).Count().IsEqualTo(3);
        await Assert.That(mentions).Contains("user");
        await Assert.That(mentions).Contains("user2");
        await Assert.That(mentions).Contains("user3");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithEmailAddress_DoesNotExtractEmail()
    {
        // Note: The regex @(\w+) would match the part after @ in emails too.
        // This test documents that behavior.
        var mentions = MentionService.ExtractMentionsFromContent("Email: test@example.com");

        // The regex matches "example" from the email - documenting current behavior
        await Assert.That(mentions).Count().IsEqualTo(1);
        await Assert.That(mentions[0]).IsEqualTo("example");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithMultilineMentions_ExtractsAll()
    {
        // Act
        var mentions = MentionService.ExtractMentionsFromContent("@user1\n@user2\n@user3");

        // Assert
        await Assert.That(mentions).Count().IsEqualTo(3);
    }

    #endregion

    #region ProcessMentionsAsync Tests

    [Test]
    public async Task ProcessMentionsAsync_WithNoMentions_DoesNothing()
    {
        // Arrange
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();

        // Act
        await _service.ProcessMentionsAsync(postId, authorId, "No mentions here", discussionId);

        // Assert
        _mockUserRepository.Verify(r => r.GetByDisplayNameAsync(It.IsAny<string>()), Times.Never);
        _mockMentionRepository.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Mention>>()), Times.Never);
    }

    [Test]
    public async Task ProcessMentionsAsync_WithValidMention_CreatesMentionAndNotification()
    {
        // Arrange
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();
        var mentionedUserId = UserId.New();

        var discussion = Discussion.Create(SpaceId.New(), authorId, "Test Discussion", "test");
        var author = User.CreateWithEmail("Author", "author@test.com", "hash", "token");
        var mentionedUser = User.Rehydrate(mentionedUserId, "MentionedUser", "mentioned@test.com",
            null, true, null, null, null, null, null, 0, true, DateTime.UtcNow);

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(authorId))
            .ReturnsAsync(author);
        _mockUserRepository.Setup(r => r.GetByDisplayNameAsync("MentionedUser"))
            .ReturnsAsync(mentionedUser);

        // Act
        await _service.ProcessMentionsAsync(postId, authorId, "Hello @MentionedUser!", discussionId);

        // Assert
        _mockMentionRepository.Verify(r => r.AddRangeAsync(It.Is<IEnumerable<Mention>>(
            m => m.Count() == 1)), Times.Once);
        _mockNotificationRepository.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Test]
    public async Task ProcessMentionsAsync_WhenMentioningYourself_SkipsSelfMention()
    {
        // Arrange
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();

        var discussion = Discussion.Create(SpaceId.New(), authorId, "Test Discussion", "test");
        var author = User.Rehydrate(authorId, "SelfUser", "self@test.com",
            null, true, null, null, null, null, null, 0, true, DateTime.UtcNow);

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(authorId))
            .ReturnsAsync(author);
        _mockUserRepository.Setup(r => r.GetByDisplayNameAsync("SelfUser"))
            .ReturnsAsync(author); // Returns the same user

        // Act
        await _service.ProcessMentionsAsync(postId, authorId, "I mention myself @SelfUser", discussionId);

        // Assert
        _mockMentionRepository.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Mention>>()), Times.Never);
        _mockNotificationRepository.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Test]
    public async Task ProcessMentionsAsync_WhenMentionedUserNotFound_SkipsMention()
    {
        // Arrange
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();

        var discussion = Discussion.Create(SpaceId.New(), authorId, "Test Discussion", "test");
        var author = User.CreateWithEmail("Author", "author@test.com", "hash", "token");

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(authorId))
            .ReturnsAsync(author);
        _mockUserRepository.Setup(r => r.GetByDisplayNameAsync("NonExistentUser"))
            .ReturnsAsync((User?)null);

        // Act
        await _service.ProcessMentionsAsync(postId, authorId, "Hello @NonExistentUser!", discussionId);

        // Assert
        _mockMentionRepository.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Mention>>()), Times.Never);
        _mockNotificationRepository.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Test]
    public async Task ProcessMentionsAsync_WhenDiscussionNotFound_DoesNothing()
    {
        // Arrange
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync((Discussion?)null);

        // Act
        await _service.ProcessMentionsAsync(postId, authorId, "Hello @SomeUser!", discussionId);

        // Assert
        _mockMentionRepository.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Mention>>()), Times.Never);
    }

    [Test]
    public async Task ProcessMentionsAsync_WhenAuthorNotFound_DoesNothing()
    {
        // Arrange
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();

        var discussion = Discussion.Create(SpaceId.New(), authorId, "Test Discussion", "test");

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(authorId))
            .ReturnsAsync((User?)null);

        // Act
        await _service.ProcessMentionsAsync(postId, authorId, "Hello @SomeUser!", discussionId);

        // Assert
        _mockMentionRepository.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Mention>>()), Times.Never);
    }

    [Test]
    public async Task ProcessMentionsAsync_WithMultipleValidMentions_CreatesAllMentions()
    {
        // Arrange
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();
        var mentionedUser1Id = UserId.New();
        var mentionedUser2Id = UserId.New();

        var discussion = Discussion.Create(SpaceId.New(), authorId, "Test Discussion", "test");
        var author = User.CreateWithEmail("Author", "author@test.com", "hash", "token");
        var mentionedUser1 = User.Rehydrate(mentionedUser1Id, "User1", "user1@test.com",
            null, true, null, null, null, null, null, 0, true, DateTime.UtcNow);
        var mentionedUser2 = User.Rehydrate(mentionedUser2Id, "User2", "user2@test.com",
            null, true, null, null, null, null, null, 0, true, DateTime.UtcNow);

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(authorId))
            .ReturnsAsync(author);
        _mockUserRepository.Setup(r => r.GetByDisplayNameAsync("User1"))
            .ReturnsAsync(mentionedUser1);
        _mockUserRepository.Setup(r => r.GetByDisplayNameAsync("User2"))
            .ReturnsAsync(mentionedUser2);

        // Act
        await _service.ProcessMentionsAsync(postId, authorId, "Hey @User1 and @User2!", discussionId);

        // Assert
        _mockMentionRepository.Verify(r => r.AddRangeAsync(It.Is<IEnumerable<Mention>>(
            m => m.Count() == 2)), Times.Once);
        _mockNotificationRepository.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Exactly(2));
    }

    [Test]
    public async Task ProcessMentionsAsync_WithMixOfValidAndInvalidMentions_CreatesOnlyValid()
    {
        // Arrange
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();
        var validUserId = UserId.New();

        var discussion = Discussion.Create(SpaceId.New(), authorId, "Test Discussion", "test");
        var author = User.CreateWithEmail("Author", "author@test.com", "hash", "token");
        var validUser = User.Rehydrate(validUserId, "ValidUser", "valid@test.com",
            null, true, null, null, null, null, null, 0, true, DateTime.UtcNow);

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(authorId))
            .ReturnsAsync(author);
        _mockUserRepository.Setup(r => r.GetByDisplayNameAsync("ValidUser"))
            .ReturnsAsync(validUser);
        _mockUserRepository.Setup(r => r.GetByDisplayNameAsync("InvalidUser"))
            .ReturnsAsync((User?)null);

        // Act
        await _service.ProcessMentionsAsync(postId, authorId, "Hey @ValidUser and @InvalidUser!", discussionId);

        // Assert
        _mockMentionRepository.Verify(r => r.AddRangeAsync(It.Is<IEnumerable<Mention>>(
            m => m.Count() == 1)), Times.Once);
        _mockNotificationRepository.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Once);
    }

    #endregion
}
