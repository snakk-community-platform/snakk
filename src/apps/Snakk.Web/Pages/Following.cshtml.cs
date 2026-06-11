using Microsoft.AspNetCore.Mvc;
using Snakk.Protos.Discussion;
using Snakk.Web.Helpers;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;

namespace Snakk.Web.Pages;

public class FollowingModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;
    private const int DiscussionPageSize = 10;

    public string ActiveTab { get; set; } = "spaces";
    public bool IsAuthenticated { get; set; }

    public int SpaceCount { get; set; }
    public int DiscussionCount { get; set; }
    public int UserCount { get; set; }

    public List<FollowingSpaceVM> Spaces { get; set; } = [];
    public List<FollowingUserVM> Users { get; set; } = [];
    public IList<RecentDiscussionInfo> FirstDiscussions { get; set; } = [];
    public bool DiscussionsHasMore { get; set; }
    public int DiscussionsNextOffset { get; set; }

    public async Task<IActionResult> OnGetAsync(string? tab, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Request.Query.TryGetValue("tab", out var queryTab) && !string.IsNullOrEmpty(queryTab))
            return RedirectToPage(new { tab = queryTab.ToString() });

        IsAuthenticated = HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName);
        if (!IsAuthenticated) return Page();

        ActiveTab = tab switch
        {
            "spaces" => "spaces",
            "users"  => "users",
            _        => "discussions"
        };

        // Fetch all ID lists in parallel — cheap, IDs only, gives us tab counts
        var spaceIdsTask      = _apiClient.GetFollowedSpacesAsync();
        var discussionIdsTask = _apiClient.GetFollowedDiscussionsAsync();
        var userIdsTask       = _apiClient.GetFollowedUsersAsync();

        try { await Task.WhenAll(spaceIdsTask, discussionIdsTask, userIdsTask); } catch { }

        var spaceIds      = spaceIdsTask.IsCompletedSuccessfully      ? spaceIdsTask.Result      : [];
        var discussionIds = discussionIdsTask.IsCompletedSuccessfully ? discussionIdsTask.Result : [];
        var userIds       = userIdsTask.IsCompletedSuccessfully       ? userIdsTask.Result       : [];

        SpaceCount      = spaceIds.Count;
        DiscussionCount = discussionIds.Count;
        UserCount       = userIds.Count;

        switch (ActiveTab)
        {
            case "spaces":
                try { Spaces = await ResolveSpacesAsync(spaceIds); } catch { }
                break;
            case "discussions":
                var batch = discussionIds.Take(DiscussionPageSize).ToList();
                DiscussionsHasMore = discussionIds.Count > DiscussionPageSize;
                DiscussionsNextOffset = DiscussionPageSize;
                if (batch.Count > 0)
                {
                    try
                    {
                        var result = await _apiClient.GetRecentDiscussionsByIdsAsync(batch);
                        FirstDiscussions = result?.Items ?? [];
                    }
                    catch { }
                }
                break;
            case "users":
                try { Users = await ResolveUsersAsync(userIds); } catch { }
                break;
        }

        return Page();
    }

    private async Task<List<FollowingSpaceVM>> ResolveSpacesAsync(List<string> ids)
    {
        if (ids.Count == 0) return [];
        var infos = await Task.WhenAll(ids.Select(id => _apiClient.GetSpaceAsync(id)));
        return ids.Zip(infos)
            .Where(x => x.Second != null)
            .Select(x =>
            {
                var s = x.Second!;
                FollowingSpaceLatestDiscussionVM? ld = null;
                if (s.LatestDiscussion != null)
                {
                    var d = s.LatestDiscussion;
                    ld = new FollowingSpaceLatestDiscussionVM
                    {
                        Href              = SnakkUrlHelper.Discussion(s.CommunitySlug, Community, s.HubSlug, s.Slug,
                                               SnakkUrlHelper.DiscussionSlugId(d.Slug, d.PublicId)),
                        Title             = d.Title,
                        AuthorDisplayName = d.AuthorDisplayName,
                        AuthorAvatarUrl   = SnakkUrlHelper.UserAvatar(d.AuthorPublicId,
                                               avatarFileName: d.HasAuthorAvatarFileName ? d.AuthorAvatarFileName : null),
                        LastActivityAt    = d.LastActivityAt?.ToDateTime()
                    };
                }
                return new FollowingSpaceVM
                {
                    PublicId        = s.PublicId,
                    Name            = s.Name,
                    Href            = SnakkUrlHelper.Space(s.CommunitySlug, Community, s.HubSlug, s.Slug),
                    AvatarUrl       = SnakkUrlHelper.SpaceAvatarThumbnail(s.PublicId,
                                           avatarFileName: s.HasAvatarFileName ? s.AvatarFileName : null,
                                           avatarThumbnailFileName: s.HasAvatarThumbnailFileName ? s.AvatarThumbnailFileName : null),
                    Description     = s.HasDescription ? s.Description : null,
                    DiscussionCount = s.DiscussionCount,
                    ReplyCount      = s.ReplyCount,
                    LatestDiscussion = ld
                };
            }).ToList();
    }

    private async Task<List<FollowingUserVM>> ResolveUsersAsync(List<string> ids)
    {
        if (ids.Count == 0) return [];
        var profiles = await Task.WhenAll(ids.Select(id => _apiClient.GetUserProfileAsync(id)));
        return ids.Zip(profiles)
            .Where(x => x.Second != null)
            .Select(x =>
            {
                var u = x.Second!;
                return new FollowingUserVM
                {
                    PublicId      = u.PublicId,
                    DisplayName   = u.DisplayName,
                    Href          = SnakkUrlHelper.UserProfile(u.PublicId),
                    AvatarUrl     = SnakkUrlHelper.UserAvatarThumbnail(u.PublicId,
                                        avatarFileName: u.HasAvatarFileName ? u.AvatarFileName : null,
                                        avatarThumbnailFileName: u.HasAvatarThumbnailFileName ? u.AvatarThumbnailFileName : null),
                    FollowerCount = u.FollowerCount
                };
            }).ToList();
    }
}
