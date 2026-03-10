using Snakk.Domain.Extensions;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

namespace Snakk.Domain.Tests.Extensions;

public class EnumConversionExtensionsTests
{
    #region ReactionType Round-Trip Tests

    [Test]
    public async Task ReactionType_Agree_ToShared_ReturnsCorrectValue()
    {
        var result = ReactionType.Agree.ToShared();
        await Assert.That(result).IsEqualTo(ReactionTypeEnum.Agree);
    }

    [Test]
    public async Task ReactionType_Love_ToShared_ReturnsCorrectValue()
    {
        var result = ReactionType.Love.ToShared();
        await Assert.That(result).IsEqualTo(ReactionTypeEnum.Love);
    }

    [Test]
    public async Task ReactionType_Watching_ToShared_ReturnsCorrectValue()
    {
        var result = ReactionType.Watching.ToShared();
        await Assert.That(result).IsEqualTo(ReactionTypeEnum.Watching);
    }

    [Test]
    public async Task ReactionType_MindBlown_ToShared_ReturnsCorrectValue()
    {
        var result = ReactionType.MindBlown.ToShared();
        await Assert.That(result).IsEqualTo(ReactionTypeEnum.MindBlown);
    }

    [Test]
    public async Task ReactionType_Agree_RoundTrips()
    {
        var original = ReactionType.Agree;
        var shared = original.ToShared();
        var roundTripped = shared.ToDomain();
        await Assert.That(roundTripped).IsEqualTo(original);
    }

    [Test]
    public async Task ReactionType_Love_RoundTrips()
    {
        var original = ReactionType.Love;
        var shared = original.ToShared();
        var roundTripped = shared.ToDomain();
        await Assert.That(roundTripped).IsEqualTo(original);
    }

    [Test]
    public async Task ReactionType_Watching_RoundTrips()
    {
        var original = ReactionType.Watching;
        var shared = original.ToShared();
        var roundTripped = shared.ToDomain();
        await Assert.That(roundTripped).IsEqualTo(original);
    }

    [Test]
    public async Task ReactionType_MindBlown_RoundTrips()
    {
        var original = ReactionType.MindBlown;
        var shared = original.ToShared();
        var roundTripped = shared.ToDomain();
        await Assert.That(roundTripped).IsEqualTo(original);
    }

    [Test]
    public async Task ReactionType_InvalidValue_ToShared_Throws()
    {
        var invalidValue = (ReactionType)999;
        await Assert.That(() => invalidValue.ToShared()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ReactionTypeEnum_InvalidValue_ToDomain_Throws()
    {
        var invalidValue = (ReactionTypeEnum)999;
        await Assert.That(() => invalidValue.ToDomain()).Throws<ArgumentOutOfRangeException>();
    }

    #endregion

    #region NotificationType Round-Trip Tests

    [Test]
    public async Task NotificationType_Mention_RoundTrips()
    {
        var original = NotificationType.Mention;
        var shared = original.ToShared();
        var roundTripped = shared.ToDomain();
        await Assert.That(shared).IsEqualTo(NotificationTypeEnum.Mention);
        await Assert.That(roundTripped).IsEqualTo(original);
    }

    [Test]
    public async Task NotificationType_Reply_RoundTrips()
    {
        var original = NotificationType.Reply;
        var shared = original.ToShared();
        var roundTripped = shared.ToDomain();
        await Assert.That(shared).IsEqualTo(NotificationTypeEnum.Reply);
        await Assert.That(roundTripped).IsEqualTo(original);
    }

    [Test]
    public async Task NotificationType_InvalidValue_ToShared_Throws()
    {
        var invalidValue = (NotificationType)999;
        await Assert.That(() => invalidValue.ToShared()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task NotificationTypeEnum_InvalidValue_ToDomain_Throws()
    {
        var invalidValue = (NotificationTypeEnum)999;
        await Assert.That(() => invalidValue.ToDomain()).Throws<ArgumentOutOfRangeException>();
    }

    #endregion

    #region CommunityVisibility Round-Trip Tests

    [Test]
    public async Task CommunityVisibility_PublicListed_RoundTrips()
    {
        var original = CommunityVisibility.PublicListed;
        var shared = original.ToShared();
        var roundTripped = shared.ToDomain();
        await Assert.That(shared).IsEqualTo(CommunityVisibilityEnum.PublicListed);
        await Assert.That(roundTripped).IsEqualTo(original);
    }

    [Test]
    public async Task CommunityVisibility_InvalidValue_ToShared_Throws()
    {
        var invalidValue = (CommunityVisibility)999;
        await Assert.That(() => invalidValue.ToShared()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task CommunityVisibilityEnum_InvalidValue_ToDomain_Throws()
    {
        var invalidValue = (CommunityVisibilityEnum)999;
        await Assert.That(() => invalidValue.ToDomain()).Throws<ArgumentOutOfRangeException>();
    }

    #endregion

    #region FollowLevel Round-Trip Tests

    [Test]
    public async Task FollowLevel_DiscussionsOnly_RoundTrips()
    {
        var original = FollowLevel.DiscussionsOnly;
        var shared = original.ToShared();
        var roundTripped = shared.ToDomain();
        await Assert.That(shared).IsEqualTo(FollowLevelEnum.DiscussionsOnly);
        await Assert.That(roundTripped).IsEqualTo(original);
    }

    [Test]
    public async Task FollowLevel_InvalidValue_ToShared_Throws()
    {
        var invalidValue = (FollowLevel)999;
        await Assert.That(() => invalidValue.ToShared()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task FollowLevelEnum_InvalidValue_ToDomain_Throws()
    {
        var invalidValue = (FollowLevelEnum)999;
        await Assert.That(() => invalidValue.ToDomain()).Throws<ArgumentOutOfRangeException>();
    }

    #endregion

    #region FollowTargetType Round-Trip Tests

    [Test]
    public async Task FollowTargetType_Discussion_RoundTrips()
    {
        var original = FollowTargetType.Discussion;
        var shared = original.ToShared();
        var roundTripped = shared.ToDomain();
        await Assert.That(shared).IsEqualTo(FollowTargetTypeEnum.Discussion);
        await Assert.That(roundTripped).IsEqualTo(original);
    }

    [Test]
    public async Task FollowTargetType_InvalidValue_ToShared_Throws()
    {
        var invalidValue = (FollowTargetType)999;
        await Assert.That(() => invalidValue.ToShared()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task FollowTargetTypeEnum_InvalidValue_ToDomain_Throws()
    {
        var invalidValue = (FollowTargetTypeEnum)999;
        await Assert.That(() => invalidValue.ToDomain()).Throws<ArgumentOutOfRangeException>();
    }

    #endregion

    #region BanType Round-Trip Tests

    [Test]
    public async Task BanType_WriteOnly_RoundTrips()
    {
        var original = BanType.WriteOnly;
        var shared = original.ToShared();
        var roundTripped = shared.ToDomain();
        await Assert.That(shared).IsEqualTo(BanTypeEnum.WriteOnly);
        await Assert.That(roundTripped).IsEqualTo(original);
    }

    [Test]
    public async Task BanType_InvalidValue_ToShared_Throws()
    {
        var invalidValue = (BanType)999;
        await Assert.That(() => invalidValue.ToShared()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task BanTypeEnum_InvalidValue_ToDomain_Throws()
    {
        var invalidValue = (BanTypeEnum)999;
        await Assert.That(() => invalidValue.ToDomain()).Throws<ArgumentOutOfRangeException>();
    }

    #endregion

    #region ReportStatus Round-Trip Tests

    [Test]
    public async Task ReportStatus_Pending_RoundTrips()
    {
        var original = ReportStatus.Pending;
        var shared = original.ToShared();
        var roundTripped = shared.ToDomain();
        await Assert.That(shared).IsEqualTo(ReportStatusEnum.Pending);
        await Assert.That(roundTripped).IsEqualTo(original);
    }

    [Test]
    public async Task ReportStatus_InvalidValue_ToShared_Throws()
    {
        var invalidValue = (ReportStatus)999;
        await Assert.That(() => invalidValue.ToShared()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ReportStatusEnum_InvalidValue_ToDomain_Throws()
    {
        var invalidValue = (ReportStatusEnum)999;
        await Assert.That(() => invalidValue.ToDomain()).Throws<ArgumentOutOfRangeException>();
    }

    #endregion

    #region UserRoleType Round-Trip Tests

    [Test]
    public async Task UserRoleType_GlobalAdmin_RoundTrips()
    {
        var original = UserRoleType.GlobalAdmin;
        var shared = original.ToShared();
        var roundTripped = shared.ToDomain();
        await Assert.That(shared).IsEqualTo(UserRoleTypeEnum.GlobalAdmin);
        await Assert.That(roundTripped).IsEqualTo(original);
    }

    [Test]
    public async Task UserRoleType_InvalidValue_ToShared_Throws()
    {
        var invalidValue = (UserRoleType)999;
        await Assert.That(() => invalidValue.ToShared()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task UserRoleTypeEnum_InvalidValue_ToDomain_Throws()
    {
        var invalidValue = (UserRoleTypeEnum)999;
        await Assert.That(() => invalidValue.ToDomain()).Throws<ArgumentOutOfRangeException>();
    }

    #endregion
}
