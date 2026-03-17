using Snakk.Protos.Community;
using Snakk.Protos.Hub;
using Snakk.Protos.Moderation;
using Snakk.Protos.Space;
using Snakk.Protos.Statistics;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.ViewModels;

public record SidebarTrendingDiscussionsVM(
    TopActiveDiscussionsList? Discussions,
    ICommunityContext Community,
    string CacheSource);

public record SidebarTrendingSpacesVM(
    TopActiveSpacesList? Spaces,
    ICommunityContext Community,
    string CacheSource);

public record SidebarTrendingContributorsVM(
    TopContributorsList? Contributors,
    ICommunityContext Community,
    string CacheSource);

public record SidebarPlatformStatsVM(
    int SpaceCount,
    int DiscussionCount,
    int ReplyCount,
    string CacheSource);

public record SidebarSpaceRulesVM(
    SpaceRulesResponse? Rules,
    ICommunityContext Community,
    string HubSlug,
    string CommunitySlug,
    bool ParentHubHasRules,
    bool ParentCommunityHasRules,
    string CacheSource);

public record SidebarHubRulesVM(
    HubRulesResponse? Rules,
    ICommunityContext Community,
    string CommunitySlug,
    bool ParentCommunityHasRules,
    string CacheSource);

public record SidebarCommunityRulesVM(
    CommunityRulesResponse? Rules,
    string CacheSource);

public record SidebarSiteRulesVM(
    SiteRulesResponse? Rules,
    string CacheSource);

public record SidebarModeratorsVM(
    GetModeratorsResponse? Moderators,
    string ModeratorsPageUrl,
    string CacheSource);
