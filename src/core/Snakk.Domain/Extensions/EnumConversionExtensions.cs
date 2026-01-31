using Snakk.Shared.Enums;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Extensions;

/// <summary>
/// Extension methods for converting between Domain enums (0-based) and Shared enums (1-based).
/// These are used during the transition period from string-based to integer-based enum storage.
/// </summary>
public static class EnumConversionExtensions
{
    // ReactionType conversions
    public static ReactionTypeEnum ToShared(this ReactionType domainEnum)
    {
        return domainEnum switch
        {
            ReactionType.ThumbsUp => ReactionTypeEnum.ThumbsUp,
            ReactionType.Heart => ReactionTypeEnum.Heart,
            ReactionType.Eyes => ReactionTypeEnum.Eyes,
            _ => throw new ArgumentOutOfRangeException(nameof(domainEnum), domainEnum, "Unknown ReactionType")
        };
    }

    public static ReactionType ToDomain(this ReactionTypeEnum sharedEnum)
    {
        return sharedEnum switch
        {
            ReactionTypeEnum.ThumbsUp => ReactionType.ThumbsUp,
            ReactionTypeEnum.Heart => ReactionType.Heart,
            ReactionTypeEnum.Eyes => ReactionType.Eyes,
            _ => throw new ArgumentOutOfRangeException(nameof(sharedEnum), sharedEnum, "Unknown ReactionTypeEnum")
        };
    }

    // NotificationType conversions
    public static NotificationTypeEnum ToShared(this NotificationType domainEnum)
    {
        return domainEnum switch
        {
            NotificationType.Mention => NotificationTypeEnum.Mention,
            NotificationType.Reply => NotificationTypeEnum.Reply,
            NotificationType.NewPostInFollowedDiscussion => NotificationTypeEnum.NewPostInFollowedDiscussion,
            NotificationType.NewDiscussionInFollowedSpace => NotificationTypeEnum.NewDiscussionInFollowedSpace,
            _ => throw new ArgumentOutOfRangeException(nameof(domainEnum), domainEnum, "Unknown NotificationType")
        };
    }

    public static NotificationType ToDomain(this NotificationTypeEnum sharedEnum)
    {
        return sharedEnum switch
        {
            NotificationTypeEnum.Mention => NotificationType.Mention,
            NotificationTypeEnum.Reply => NotificationType.Reply,
            NotificationTypeEnum.NewPostInFollowedDiscussion => NotificationType.NewPostInFollowedDiscussion,
            NotificationTypeEnum.NewDiscussionInFollowedSpace => NotificationType.NewDiscussionInFollowedSpace,
            _ => throw new ArgumentOutOfRangeException(nameof(sharedEnum), sharedEnum, "Unknown NotificationTypeEnum")
        };
    }

    // CommunityVisibility conversions
    public static CommunityVisibilityEnum ToShared(this CommunityVisibility domainEnum)
    {
        return domainEnum switch
        {
            CommunityVisibility.PublicListed => CommunityVisibilityEnum.PublicListed,
            CommunityVisibility.PublicUnlisted => CommunityVisibilityEnum.PublicUnlisted,
            _ => throw new ArgumentOutOfRangeException(nameof(domainEnum), domainEnum, "Unknown CommunityVisibility")
        };
    }

    public static CommunityVisibility ToDomain(this CommunityVisibilityEnum sharedEnum)
    {
        return sharedEnum switch
        {
            CommunityVisibilityEnum.PublicListed => CommunityVisibility.PublicListed,
            CommunityVisibilityEnum.PublicUnlisted => CommunityVisibility.PublicUnlisted,
            _ => throw new ArgumentOutOfRangeException(nameof(sharedEnum), sharedEnum, "Unknown CommunityVisibilityEnum")
        };
    }

    // FollowLevel conversions
    public static FollowLevelEnum ToShared(this FollowLevel domainEnum)
    {
        return domainEnum switch
        {
            FollowLevel.DiscussionsOnly => FollowLevelEnum.DiscussionsOnly,
            FollowLevel.DiscussionsAndPosts => FollowLevelEnum.DiscussionsAndPosts,
            _ => throw new ArgumentOutOfRangeException(nameof(domainEnum), domainEnum, "Unknown FollowLevel")
        };
    }

    public static FollowLevel ToDomain(this FollowLevelEnum sharedEnum)
    {
        return sharedEnum switch
        {
            FollowLevelEnum.DiscussionsOnly => FollowLevel.DiscussionsOnly,
            FollowLevelEnum.DiscussionsAndPosts => FollowLevel.DiscussionsAndPosts,
            _ => throw new ArgumentOutOfRangeException(nameof(sharedEnum), sharedEnum, "Unknown FollowLevelEnum")
        };
    }

    // FollowTargetType conversions
    public static FollowTargetTypeEnum ToShared(this FollowTargetType domainEnum)
    {
        return domainEnum switch
        {
            FollowTargetType.Discussion => FollowTargetTypeEnum.Discussion,
            FollowTargetType.Space => FollowTargetTypeEnum.Space,
            FollowTargetType.User => FollowTargetTypeEnum.User,
            _ => throw new ArgumentOutOfRangeException(nameof(domainEnum), domainEnum, "Unknown FollowTargetType")
        };
    }

    public static FollowTargetType ToDomain(this FollowTargetTypeEnum sharedEnum)
    {
        return sharedEnum switch
        {
            FollowTargetTypeEnum.Discussion => FollowTargetType.Discussion,
            FollowTargetTypeEnum.Space => FollowTargetType.Space,
            FollowTargetTypeEnum.User => FollowTargetType.User,
            _ => throw new ArgumentOutOfRangeException(nameof(sharedEnum), sharedEnum, "Unknown FollowTargetTypeEnum")
        };
    }

    // BanType conversions
    public static BanTypeEnum ToShared(this BanType domainEnum)
    {
        return domainEnum switch
        {
            BanType.WriteOnly => BanTypeEnum.WriteOnly,
            BanType.ReadWrite => BanTypeEnum.ReadWrite,
            _ => throw new ArgumentOutOfRangeException(nameof(domainEnum), domainEnum, "Unknown BanType")
        };
    }

    public static BanType ToDomain(this BanTypeEnum sharedEnum)
    {
        return sharedEnum switch
        {
            BanTypeEnum.WriteOnly => BanType.WriteOnly,
            BanTypeEnum.ReadWrite => BanType.ReadWrite,
            _ => throw new ArgumentOutOfRangeException(nameof(sharedEnum), sharedEnum, "Unknown BanTypeEnum")
        };
    }

    // ReportStatus conversions
    public static ReportStatusEnum ToShared(this ReportStatus domainEnum)
    {
        return domainEnum switch
        {
            ReportStatus.Pending => ReportStatusEnum.Pending,
            ReportStatus.Resolved => ReportStatusEnum.Resolved,
            ReportStatus.Dismissed => ReportStatusEnum.Dismissed,
            _ => throw new ArgumentOutOfRangeException(nameof(domainEnum), domainEnum, "Unknown ReportStatus")
        };
    }

    public static ReportStatus ToDomain(this ReportStatusEnum sharedEnum)
    {
        return sharedEnum switch
        {
            ReportStatusEnum.Pending => ReportStatus.Pending,
            ReportStatusEnum.Resolved => ReportStatus.Resolved,
            ReportStatusEnum.Dismissed => ReportStatus.Dismissed,
            _ => throw new ArgumentOutOfRangeException(nameof(sharedEnum), sharedEnum, "Unknown ReportStatusEnum")
        };
    }

    // UserRoleType conversions
    public static UserRoleTypeEnum ToShared(this UserRoleType domainEnum)
    {
        return domainEnum switch
        {
            UserRoleType.GlobalAdmin => UserRoleTypeEnum.GlobalAdmin,
            UserRoleType.CommunityAdmin => UserRoleTypeEnum.CommunityAdmin,
            UserRoleType.CommunityMod => UserRoleTypeEnum.CommunityMod,
            UserRoleType.HubMod => UserRoleTypeEnum.HubMod,
            UserRoleType.SpaceMod => UserRoleTypeEnum.SpaceMod,
            _ => throw new ArgumentOutOfRangeException(nameof(domainEnum), domainEnum, "Unknown UserRoleType")
        };
    }

    public static UserRoleType ToDomain(this UserRoleTypeEnum sharedEnum)
    {
        return sharedEnum switch
        {
            UserRoleTypeEnum.GlobalAdmin => UserRoleType.GlobalAdmin,
            UserRoleTypeEnum.CommunityAdmin => UserRoleType.CommunityAdmin,
            UserRoleTypeEnum.CommunityMod => UserRoleType.CommunityMod,
            UserRoleTypeEnum.HubMod => UserRoleType.HubMod,
            UserRoleTypeEnum.SpaceMod => UserRoleType.SpaceMod,
            _ => throw new ArgumentOutOfRangeException(nameof(sharedEnum), sharedEnum, "Unknown UserRoleTypeEnum")
        };
    }
}
