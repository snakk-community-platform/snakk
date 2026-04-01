using NSubstitute;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;

namespace Snakk.Application.Tests.Services;

public class MentionServiceTests
{
    private readonly IMentionRepository _mentionRepository = Substitute.For<IMentionRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDiscussionRepository _discussionRepository = Substitute.For<IDiscussionRepository>();
    private readonly INotificationRepository _notificationRepository = Substitute.For<INotificationRepository>();
    private readonly IRealtimeNotifier _realtimeNotifier = Substitute.For<IRealtimeNotifier>();
    private readonly ICounterService _counterService = Substitute.For<ICounterService>();
    private MentionService _service = null!;
    private NotificationUseCase _notificationUseCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _notificationUseCase = new NotificationUseCase(
            _notificationRepository,
            _realtimeNotifier,
            _counterService);

        _service = new MentionService(
            _mentionRepository,
            _userRepository,
            _discussionRepository,
            _notificationUseCase);
    }

    #region ExtractMentionsFromContent Tests

    [Test]
    public async Task ExtractMentionsFromContent_WithSingleMention_ExtractsMention()
    {
        var mentions = MentionService.ExtractMentionsFromContent("Hello @JohnDoe how are you?");
        await Assert.That(mentions).Count().IsEqualTo(1);
        await Assert.That(mentions[0]).IsEqualTo("JohnDoe");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithMultipleMentions_ExtractsAll()
    {
        var mentions = MentionService.ExtractMentionsFromContent("Hey @Alice and @Bob, check this out @Charlie");
        await Assert.That(mentions).Count().IsEqualTo(3);
        await Assert.That(mentions).Contains("Alice");
        await Assert.That(mentions).Contains("Bob");
        await Assert.That(mentions).Contains("Charlie");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithDuplicateMentions_ReturnsDistinct()
    {
        var mentions = MentionService.ExtractMentionsFromContent("@Alice mentioned @Alice again @Alice");
        await Assert.That(mentions).Count().IsEqualTo(1);
        await Assert.That(mentions[0]).IsEqualTo("Alice");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithNoMentions_ReturnsEmptyList()
    {
        var mentions = MentionService.ExtractMentionsFromContent("No mentions here at all");
        await Assert.That(mentions).Count().IsEqualTo(0);
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithEmptyString_ReturnsEmptyList()
    {
        var mentions = MentionService.ExtractMentionsFromContent("");
        await Assert.That(mentions).Count().IsEqualTo(0);
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithMentionAtStart_ExtractsMention()
    {
        var mentions = MentionService.ExtractMentionsFromContent("@StartUser is the first word");
        await Assert.That(mentions).Count().IsEqualTo(1);
        await Assert.That(mentions[0]).IsEqualTo("StartUser");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithMentionAtEnd_ExtractsMention()
    {
        var mentions = MentionService.ExtractMentionsFromContent("Last mention is @EndUser");
        await Assert.That(mentions).Count().IsEqualTo(1);
        await Assert.That(mentions[0]).IsEqualTo("EndUser");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithUnderscoreInUsername_ExtractsFull()
    {
        var mentions = MentionService.ExtractMentionsFromContent("Hi @user_name_123 welcome");
        await Assert.That(mentions).Count().IsEqualTo(1);
        await Assert.That(mentions[0]).IsEqualTo("user_name_123");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithSpecialCharsAfterMention_ExtractsCorrectly()
    {
        var mentions = MentionService.ExtractMentionsFromContent("@user, @user2. @user3!");
        await Assert.That(mentions).Count().IsEqualTo(3);
        await Assert.That(mentions).Contains("user");
        await Assert.That(mentions).Contains("user2");
        await Assert.That(mentions).Contains("user3");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithEmailAddress_DoesNotExtractEmail()
    {
        var mentions = MentionService.ExtractMentionsFromContent("Email: test@example.com");
        await Assert.That(mentions).Count().IsEqualTo(1);
        await Assert.That(mentions[0]).IsEqualTo("example");
    }

    [Test]
    public async Task ExtractMentionsFromContent_WithMultilineMentions_ExtractsAll()
    {
        var mentions = MentionService.ExtractMentionsFromContent("@user1\n@user2\n@user3");
        await Assert.That(mentions).Count().IsEqualTo(3);
    }

    #endregion

    #region ProcessMentionsAsync Tests

    [Test]
    public async Task ProcessMentionsAsync_WithNoMentions_DoesNothing()
    {
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();

        await _service.ProcessMentionsAsync(postId, authorId, "No mentions here", discussionId);

        await _userRepository.DidNotReceive().GetByDisplayNameAsync(Arg.Any<string>());
        await _mentionRepository.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<Mention>>());
    }

    [Test]
    public async Task ProcessMentionsAsync_WithValidMention_CreatesMentionAndNotification()
    {
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();
        var mentionedUserId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), authorId, "Test Discussion", "test");
        var author = User.CreateWithEmail("Author", "author@test.com", "hash", "token");
        var mentionedUser = User.Rehydrate(mentionedUserId, "MentionedUser", "mentioned@test.com", null, true, null, null, null, null, null, 0, true, DateTime.UtcNow);

        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);
        _userRepository.GetByPublicIdAsync(authorId).Returns(author);
        _userRepository.GetByDisplayNameAsync("MentionedUser").Returns(mentionedUser);

        await _service.ProcessMentionsAsync(postId, authorId, "Hello @MentionedUser!", discussionId);

        await _mentionRepository.Received(1).AddRangeAsync(Arg.Is<IEnumerable<Mention>>(m => m.Count() == 1));
        await _notificationRepository.Received(1).AddAsync(Arg.Any<Notification>());
    }

    [Test]
    public async Task ProcessMentionsAsync_WhenMentioningYourself_SkipsSelfMention()
    {
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();
        var discussion = Discussion.Create(SpaceId.New(), authorId, "Test Discussion", "test");
        var author = User.Rehydrate(authorId, "SelfUser", "self@test.com", null, true, null, null, null, null, null, 0, true, DateTime.UtcNow);

        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);
        _userRepository.GetByPublicIdAsync(authorId).Returns(author);
        _userRepository.GetByDisplayNameAsync("SelfUser").Returns(author);

        await _service.ProcessMentionsAsync(postId, authorId, "I mention myself @SelfUser", discussionId);

        await _mentionRepository.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<Mention>>());
        await _notificationRepository.DidNotReceive().AddAsync(Arg.Any<Notification>());
    }

    [Test]
    public async Task ProcessMentionsAsync_WhenMentionedUserNotFound_SkipsMention()
    {
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();
        var discussion = Discussion.Create(SpaceId.New(), authorId, "Test Discussion", "test");
        var author = User.CreateWithEmail("Author", "author@test.com", "hash", "token");

        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);
        _userRepository.GetByPublicIdAsync(authorId).Returns(author);
        _userRepository.GetByDisplayNameAsync("NonExistentUser").Returns((User?)null);

        await _service.ProcessMentionsAsync(postId, authorId, "Hello @NonExistentUser!", discussionId);

        await _mentionRepository.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<Mention>>());
        await _notificationRepository.DidNotReceive().AddAsync(Arg.Any<Notification>());
    }

    [Test]
    public async Task ProcessMentionsAsync_WhenDiscussionNotFound_DoesNothing()
    {
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();

        _discussionRepository.GetByPublicIdAsync(discussionId).Returns((Discussion?)null);

        await _service.ProcessMentionsAsync(postId, authorId, "Hello @SomeUser!", discussionId);

        await _mentionRepository.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<Mention>>());
    }

    [Test]
    public async Task ProcessMentionsAsync_WhenAuthorNotFound_DoesNothing()
    {
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();
        var discussion = Discussion.Create(SpaceId.New(), authorId, "Test Discussion", "test");

        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);
        _userRepository.GetByPublicIdAsync(authorId).Returns((User?)null);

        await _service.ProcessMentionsAsync(postId, authorId, "Hello @SomeUser!", discussionId);

        await _mentionRepository.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<Mention>>());
    }

    [Test]
    public async Task ProcessMentionsAsync_WithMultipleValidMentions_CreatesAllMentions()
    {
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();
        var mentionedUser1Id = UserId.New();
        var mentionedUser2Id = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), authorId, "Test Discussion", "test");
        var author = User.CreateWithEmail("Author", "author@test.com", "hash", "token");
        var mentionedUser1 = User.Rehydrate(mentionedUser1Id, "User1", "user1@test.com", null, true, null, null, null, null, null, 0, true, DateTime.UtcNow);
        var mentionedUser2 = User.Rehydrate(mentionedUser2Id, "User2", "user2@test.com", null, true, null, null, null, null, null, 0, true, DateTime.UtcNow);

        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);
        _userRepository.GetByPublicIdAsync(authorId).Returns(author);
        _userRepository.GetByDisplayNameAsync("User1").Returns(mentionedUser1);
        _userRepository.GetByDisplayNameAsync("User2").Returns(mentionedUser2);

        await _service.ProcessMentionsAsync(postId, authorId, "Hey @User1 and @User2!", discussionId);

        await _mentionRepository.Received(1).AddRangeAsync(Arg.Is<IEnumerable<Mention>>(m => m.Count() == 2));
        await _notificationRepository.Received(2).AddAsync(Arg.Any<Notification>());
    }

    [Test]
    public async Task ProcessMentionsAsync_WithMixOfValidAndInvalidMentions_CreatesOnlyValid()
    {
        var postId = PostId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();
        var validUserId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), authorId, "Test Discussion", "test");
        var author = User.CreateWithEmail("Author", "author@test.com", "hash", "token");
        var validUser = User.Rehydrate(validUserId, "ValidUser", "valid@test.com", null, true, null, null, null, null, null, 0, true, DateTime.UtcNow);

        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);
        _userRepository.GetByPublicIdAsync(authorId).Returns(author);
        _userRepository.GetByDisplayNameAsync("ValidUser").Returns(validUser);
        _userRepository.GetByDisplayNameAsync("InvalidUser").Returns((User?)null);

        await _service.ProcessMentionsAsync(postId, authorId, "Hey @ValidUser and @InvalidUser!", discussionId);

        await _mentionRepository.Received(1).AddRangeAsync(Arg.Is<IEnumerable<Mention>>(m => m.Count() == 1));
        await _notificationRepository.Received(1).AddAsync(Arg.Any<Notification>());
    }

    #endregion
}
