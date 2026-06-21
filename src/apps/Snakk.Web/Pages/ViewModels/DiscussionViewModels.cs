using Snakk.Protos.Discussion;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.ViewModels;

public record DiscussionListItemVM(
    RecentDiscussionInfo Discussion,
    bool ShowCommunity,
    ICommunityContext Community,
    bool ShowHub = true,
    bool ShowSpace = true,
    bool ShowUser = true,
    bool ShowPath = true,
    string? UnfollowId = null,
    bool IsFirst = false,
    string? ReactionEmoji = null,
    string? UnsaveId = null,
    int? Rank = null,
    DateTime? SavedAt = null,
    bool ShowStats = false,
    DateTime? VisitedAt = null,
    string? RemoveHistoryId = null,
    DateTime? ReactedAt = null,
    DateTime? FollowedAt = null,
    bool HideInlineStats = false,
    int? UnreadCount = null,
    bool GotoUnread = false);

public record SpaceDiscussionListItemVM(
    DiscussionBySpaceInfo Discussion,
    ICommunityContext Community,
    string HubSlug,
    string SpaceSlug);
