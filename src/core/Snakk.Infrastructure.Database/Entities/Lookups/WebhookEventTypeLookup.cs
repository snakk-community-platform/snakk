namespace Snakk.Infrastructure.Database.Entities.Lookups;

/// <summary>
/// Standard webhook event types that can be subscribed to
/// </summary>
public static class WebhookEventTypeLookup
{
    // User Events
    public const string UserRegistered = "UserRegistered";
    public const string UserBanned = "UserBanned";
    public const string UserUnbanned = "UserUnbanned";
    public const string UserDeleted = "UserDeleted";
    public const string UserRoleChanged = "UserRoleChanged";
    public const string UserProfileUpdated = "UserProfileUpdated";

    // Content Events
    public const string CommunityCreated = "CommunityCreated";
    public const string CommunityUpdated = "CommunityUpdated";
    public const string CommunityDeleted = "CommunityDeleted";
    public const string DiscussionCreated = "DiscussionCreated";
    public const string DiscussionUpdated = "DiscussionUpdated";
    public const string DiscussionDeleted = "DiscussionDeleted";
    public const string PostCreated = "PostCreated";
    public const string PostUpdated = "PostUpdated";
    public const string PostDeleted = "PostDeleted";
    public const string ReactionAdded = "ReactionAdded";
    public const string ReactionRemoved = "ReactionRemoved";

    // Moderation Events
    public const string ReportCreated = "ReportCreated";
    public const string ReportResolved = "ReportResolved";
    public const string ReportDismissed = "ReportDismissed";
    public const string ContentFlagged = "ContentFlagged";
    public const string ModerationActionTaken = "ModerationActionTaken";
    public const string AutoModTriggered = "AutoModTriggered";

    // System Events
    public const string SystemSettingChanged = "SystemSettingChanged";
    public const string PerformanceAlert = "PerformanceAlert";
    public const string SecurityIncident = "SecurityIncident";
    public const string BackupCompleted = "BackupCompleted";

    /// <summary>
    /// Get all available event types
    /// </summary>
    public static string[] GetAll() => new[]
    {
        // User Events
        UserRegistered, UserBanned, UserUnbanned, UserDeleted, UserRoleChanged, UserProfileUpdated,
        
        // Content Events
        CommunityCreated, CommunityUpdated, CommunityDeleted,
        DiscussionCreated, DiscussionUpdated, DiscussionDeleted,
        PostCreated, PostUpdated, PostDeleted,
        ReactionAdded, ReactionRemoved,
        
        // Moderation Events
        ReportCreated, ReportResolved, ReportDismissed,
        ContentFlagged, ModerationActionTaken, AutoModTriggered,
        
        // System Events
        SystemSettingChanged, PerformanceAlert, SecurityIncident, BackupCompleted
    };

    /// <summary>
    /// Get event types by category
    /// </summary>
    public static class Categories
    {
        public static string[] User => new[] { UserRegistered, UserBanned, UserUnbanned, UserDeleted, UserRoleChanged, UserProfileUpdated };
        public static string[] Content => new[] { CommunityCreated, CommunityUpdated, CommunityDeleted, DiscussionCreated, DiscussionUpdated, DiscussionDeleted, PostCreated, PostUpdated, PostDeleted, ReactionAdded, ReactionRemoved };
        public static string[] Moderation => new[] { ReportCreated, ReportResolved, ReportDismissed, ContentFlagged, ModerationActionTaken, AutoModTriggered };
        public static string[] System => new[] { SystemSettingChanged, PerformanceAlert, SecurityIncident, BackupCompleted };
    }
}
