using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.ValueObjects;

public class ValueObjectTests
{
    #region UserId Tests

    [Test]
    public async Task UserId_New_CreatesUniqueValues()
    {
        // Act
        var id1 = UserId.New();
        var id2 = UserId.New();

        // Assert
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task UserId_From_CreatesWithGivenValue()
    {
        // Act
        var id = UserId.From("test-user-id");

        // Assert
        await Assert.That(id.Value).IsEqualTo("test-user-id");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task UserId_From_EmptyOrNull_ThrowsArgumentException(string? emptyValue)
    {
        // Act & Assert
        await Assert.That(() => UserId.From(emptyValue!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task UserId_ToString_ReturnsValue()
    {
        // Arrange
        var id = UserId.From("my-user");

        // Act & Assert
        await Assert.That(id.ToString()).IsEqualTo("my-user");
    }

    [Test]
    public async Task UserId_ImplicitStringConversion_Works()
    {
        // Arrange
        var id = UserId.From("my-user");

        // Act
        string value = id;

        // Assert
        await Assert.That(value).IsEqualTo("my-user");
    }

    [Test]
    public async Task UserId_TwoIdsWithSameValue_AreEqual()
    {
        // Arrange
        var id1 = UserId.From("same-value");
        var id2 = UserId.From("same-value");

        // Assert
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region PostId Tests

    [Test]
    public async Task PostId_New_CreatesUniqueValues()
    {
        var id1 = PostId.New();
        var id2 = PostId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task PostId_From_CreatesWithGivenValue()
    {
        var id = PostId.From("test-post-id");
        await Assert.That(id.Value).IsEqualTo("test-post-id");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task PostId_From_EmptyOrNull_ThrowsArgumentException(string? emptyValue)
    {
        await Assert.That(() => PostId.From(emptyValue!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task PostId_ToString_ReturnsValue()
    {
        var id = PostId.From("my-post");
        await Assert.That(id.ToString()).IsEqualTo("my-post");
    }

    [Test]
    public async Task PostId_ImplicitStringConversion_Works()
    {
        var id = PostId.From("my-post");
        string value = id;
        await Assert.That(value).IsEqualTo("my-post");
    }

    [Test]
    public async Task PostId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = PostId.From("same");
        var id2 = PostId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region DiscussionId Tests

    [Test]
    public async Task DiscussionId_New_CreatesUniqueValues()
    {
        var id1 = DiscussionId.New();
        var id2 = DiscussionId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task DiscussionId_From_CreatesWithGivenValue()
    {
        var id = DiscussionId.From("test-discussion");
        await Assert.That(id.Value).IsEqualTo("test-discussion");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task DiscussionId_From_EmptyOrNull_ThrowsArgumentException(string? emptyValue)
    {
        await Assert.That(() => DiscussionId.From(emptyValue!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task DiscussionId_ToString_ReturnsValue()
    {
        var id = DiscussionId.From("disc-id");
        await Assert.That(id.ToString()).IsEqualTo("disc-id");
    }

    [Test]
    public async Task DiscussionId_ImplicitStringConversion_Works()
    {
        var id = DiscussionId.From("disc-id");
        string value = id;
        await Assert.That(value).IsEqualTo("disc-id");
    }

    [Test]
    public async Task DiscussionId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = DiscussionId.From("same");
        var id2 = DiscussionId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region CommunityId Tests

    [Test]
    public async Task CommunityId_New_CreatesUniqueValues()
    {
        var id1 = CommunityId.New();
        var id2 = CommunityId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task CommunityId_From_CreatesWithGivenValue()
    {
        var id = CommunityId.From("test-community");
        await Assert.That(id.Value).IsEqualTo("test-community");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task CommunityId_From_EmptyOrNull_ThrowsArgumentException(string? emptyValue)
    {
        await Assert.That(() => CommunityId.From(emptyValue!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task CommunityId_ToString_ReturnsValue()
    {
        var id = CommunityId.From("comm-id");
        await Assert.That(id.ToString()).IsEqualTo("comm-id");
    }

    [Test]
    public async Task CommunityId_ImplicitStringConversion_Works()
    {
        var id = CommunityId.From("comm-id");
        string value = id;
        await Assert.That(value).IsEqualTo("comm-id");
    }

    [Test]
    public async Task CommunityId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = CommunityId.From("same");
        var id2 = CommunityId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region HubId Tests

    [Test]
    public async Task HubId_New_CreatesUniqueValues()
    {
        var id1 = HubId.New();
        var id2 = HubId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task HubId_From_CreatesWithGivenValue()
    {
        var id = HubId.From("test-hub");
        await Assert.That(id.Value).IsEqualTo("test-hub");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task HubId_From_EmptyOrNull_ThrowsArgumentException(string? emptyValue)
    {
        await Assert.That(() => HubId.From(emptyValue!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task HubId_ToString_ReturnsValue()
    {
        var id = HubId.From("hub-id");
        await Assert.That(id.ToString()).IsEqualTo("hub-id");
    }

    [Test]
    public async Task HubId_ImplicitStringConversion_Works()
    {
        var id = HubId.From("hub-id");
        string value = id;
        await Assert.That(value).IsEqualTo("hub-id");
    }

    [Test]
    public async Task HubId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = HubId.From("same");
        var id2 = HubId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region SpaceId Tests

    [Test]
    public async Task SpaceId_New_CreatesUniqueValues()
    {
        var id1 = SpaceId.New();
        var id2 = SpaceId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task SpaceId_From_CreatesWithGivenValue()
    {
        var id = SpaceId.From("test-space");
        await Assert.That(id.Value).IsEqualTo("test-space");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task SpaceId_From_EmptyOrNull_ThrowsArgumentException(string? emptyValue)
    {
        await Assert.That(() => SpaceId.From(emptyValue!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task SpaceId_ToString_ReturnsValue()
    {
        var id = SpaceId.From("space-id");
        await Assert.That(id.ToString()).IsEqualTo("space-id");
    }

    [Test]
    public async Task SpaceId_ImplicitStringConversion_Works()
    {
        var id = SpaceId.From("space-id");
        string value = id;
        await Assert.That(value).IsEqualTo("space-id");
    }

    [Test]
    public async Task SpaceId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = SpaceId.From("same");
        var id2 = SpaceId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region ReactionId Tests

    [Test]
    public async Task ReactionId_New_CreatesUniqueValues()
    {
        var id1 = ReactionId.New();
        var id2 = ReactionId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task ReactionId_From_CreatesWithGivenValue()
    {
        var id = ReactionId.From("test-reaction");
        await Assert.That(id.Value).IsEqualTo("test-reaction");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task ReactionId_From_EmptyOrNull_ThrowsArgumentException(string? emptyValue)
    {
        await Assert.That(() => ReactionId.From(emptyValue!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task ReactionId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = ReactionId.From("same");
        var id2 = ReactionId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region NotificationId Tests

    [Test]
    public async Task NotificationId_New_CreatesUniqueValues()
    {
        var id1 = NotificationId.New();
        var id2 = NotificationId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task NotificationId_From_CreatesWithGivenValue()
    {
        var id = NotificationId.From("test-notif");
        await Assert.That(id.Value).IsEqualTo("test-notif");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task NotificationId_From_EmptyOrNull_ThrowsArgumentException(string? emptyValue)
    {
        await Assert.That(() => NotificationId.From(emptyValue!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task NotificationId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = NotificationId.From("same");
        var id2 = NotificationId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region FollowId Tests

    [Test]
    public async Task FollowId_New_CreatesUniqueValues()
    {
        var id1 = FollowId.New();
        var id2 = FollowId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task FollowId_From_CreatesWithGivenValue()
    {
        var id = FollowId.From("test-follow");
        await Assert.That(id.Value).IsEqualTo("test-follow");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task FollowId_From_EmptyOrNull_ThrowsArgumentException(string? emptyValue)
    {
        await Assert.That(() => FollowId.From(emptyValue!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task FollowId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = FollowId.From("same");
        var id2 = FollowId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region MentionId Tests

    [Test]
    public async Task MentionId_New_CreatesUniqueValues()
    {
        var id1 = MentionId.New();
        var id2 = MentionId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task MentionId_From_CreatesWithGivenValue()
    {
        var id = MentionId.From("test-mention");
        await Assert.That(id.Value).IsEqualTo("test-mention");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task MentionId_From_EmptyOrNull_ThrowsArgumentException(string? emptyValue)
    {
        await Assert.That(() => MentionId.From(emptyValue!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task MentionId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = MentionId.From("same");
        var id2 = MentionId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region CommunityDomainId Tests

    [Test]
    public async Task CommunityDomainId_New_CreatesUniqueValues()
    {
        var id1 = CommunityDomainId.New();
        var id2 = CommunityDomainId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task CommunityDomainId_From_CreatesWithGivenValue()
    {
        var id = CommunityDomainId.From("test-domain");
        await Assert.That(id.Value).IsEqualTo("test-domain");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task CommunityDomainId_From_EmptyOrNull_ThrowsArgumentException(string? emptyValue)
    {
        await Assert.That(() => CommunityDomainId.From(emptyValue!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task CommunityDomainId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = CommunityDomainId.From("same");
        var id2 = CommunityDomainId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region AchievementId Tests

    [Test]
    public async Task AchievementId_New_CreatesUniqueValues()
    {
        var id1 = AchievementId.New();
        var id2 = AchievementId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task AchievementId_From_CreatesWithGivenValue()
    {
        var id = AchievementId.From("test-achievement");
        await Assert.That(id.Value).IsEqualTo("test-achievement");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task AchievementId_From_EmptyOrNull_ThrowsArgumentException(string? emptyValue)
    {
        await Assert.That(() => AchievementId.From(emptyValue!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task AchievementId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = AchievementId.From("same");
        var id2 = AchievementId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region UserAchievementId Tests

    [Test]
    public async Task UserAchievementId_New_CreatesUniqueValues()
    {
        var id1 = UserAchievementId.New();
        var id2 = UserAchievementId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task UserAchievementId_From_CreatesWithGivenValue()
    {
        var id = UserAchievementId.From("test-user-achievement");
        await Assert.That(id.Value).IsEqualTo("test-user-achievement");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task UserAchievementId_From_EmptyOrNull_ThrowsArgumentException(string? emptyValue)
    {
        await Assert.That(() => UserAchievementId.From(emptyValue!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task UserAchievementId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = UserAchievementId.From("same");
        var id2 = UserAchievementId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region UserRoleId Tests

    [Test]
    public async Task UserRoleId_New_CreatesUniqueValues()
    {
        var id1 = UserRoleId.New();
        var id2 = UserRoleId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task UserRoleId_From_CreatesWithGivenValue()
    {
        var id = UserRoleId.From("test-role");
        await Assert.That(id.Value).IsEqualTo("test-role");
    }

    [Test]
    public async Task UserRoleId_ImplicitStringConversion_Works()
    {
        var id = UserRoleId.From("role-id");
        string value = id;
        await Assert.That(value).IsEqualTo("role-id");
    }

    [Test]
    public async Task UserRoleId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = UserRoleId.From("same");
        var id2 = UserRoleId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region UserBanId Tests

    [Test]
    public async Task UserBanId_New_CreatesUniqueValues()
    {
        var id1 = UserBanId.New();
        var id2 = UserBanId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task UserBanId_From_CreatesWithGivenValue()
    {
        var id = UserBanId.From("test-ban");
        await Assert.That(id.Value).IsEqualTo("test-ban");
    }

    [Test]
    public async Task UserBanId_ImplicitStringConversion_Works()
    {
        var id = UserBanId.From("ban-id");
        string value = id;
        await Assert.That(value).IsEqualTo("ban-id");
    }

    [Test]
    public async Task UserBanId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = UserBanId.From("same");
        var id2 = UserBanId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region ReportId Tests

    [Test]
    public async Task ReportId_New_CreatesUniqueValues()
    {
        var id1 = ReportId.New();
        var id2 = ReportId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task ReportId_From_CreatesWithGivenValue()
    {
        var id = ReportId.From("test-report");
        await Assert.That(id.Value).IsEqualTo("test-report");
    }

    [Test]
    public async Task ReportId_ImplicitStringConversion_Works()
    {
        var id = ReportId.From("report-id");
        string value = id;
        await Assert.That(value).IsEqualTo("report-id");
    }

    [Test]
    public async Task ReportId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = ReportId.From("same");
        var id2 = ReportId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion

    #region ReportReasonId Tests

    [Test]
    public async Task ReportReasonId_New_CreatesUniqueValues()
    {
        var id1 = ReportReasonId.New();
        var id2 = ReportReasonId.New();
        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task ReportReasonId_From_CreatesWithGivenValue()
    {
        var id = ReportReasonId.From("test-reason");
        await Assert.That(id.Value).IsEqualTo("test-reason");
    }

    [Test]
    public async Task ReportReasonId_ImplicitStringConversion_Works()
    {
        var id = ReportReasonId.From("reason-id");
        string value = id;
        await Assert.That(value).IsEqualTo("reason-id");
    }

    [Test]
    public async Task ReportReasonId_TwoIdsWithSameValue_AreEqual()
    {
        var id1 = ReportReasonId.From("same");
        var id2 = ReportReasonId.From("same");
        await Assert.That(id1).IsEqualTo(id2);
    }

    #endregion
}
