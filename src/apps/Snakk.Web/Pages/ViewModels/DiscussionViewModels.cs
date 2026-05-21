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
    string? UnfollowId = null);

public record SpaceDiscussionListItemVM(
    DiscussionBySpaceInfo Discussion,
    ICommunityContext Community,
    string HubSlug,
    string SpaceSlug);
