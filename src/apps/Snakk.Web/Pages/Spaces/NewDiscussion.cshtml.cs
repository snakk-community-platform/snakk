using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Helpers;
using Snakk.Web.Services;
using Snakk.Protos.Hub;
using Snakk.Protos.Space;
using Snakk.Protos.Community;

namespace Snakk.Web.Pages.Spaces;

/// <summary>
/// Type picker page — shows cards for each allowed discussion type.
/// </summary>
public class NewDiscussionModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public SpaceInfo? Space { get; set; }
    public HubInfo? Hub { get; set; }
    public CommunityInfo? CommunityDetail { get; set; }
    public string HubSlug { get; set; } = string.Empty;
    public string SpaceSlug { get; set; } = string.Empty;
    public List<int> AllowedTypes { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(string hubSlug, string spaceSlug)
    {
        HubSlug = hubSlug;
        SpaceSlug = spaceSlug;

        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
        {
            var returnUrl = SnakkUrlHelper.NewDiscussion(CommunityContext, hubSlug, spaceSlug);
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var hubTask = _apiClient.GetHubBySlugResultAsync(hubSlug, CommunityContext.CommunitySlug!);
        var spaceTask = _apiClient.GetSpaceBySlugResultAsync(spaceSlug, hubSlug);
        var communityTask = !string.IsNullOrEmpty(CommunityContext.CommunitySlug)
            ? _apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug)
            : Task.FromResult<CommunityInfo?>(null);

        await Task.WhenAll(hubTask, spaceTask, communityTask);

        Hub = hubTask.Result.IsSuccess ? hubTask.Result.Value : null;
        CommunityDetail = communityTask.IsCompletedSuccessfully ? communityTask.Result : null;

        if (!spaceTask.Result.IsSuccess)
            return spaceTask.Result.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        Space = spaceTask.Result.Value!;
        // Exclude Announcement (3) — created from admin panel only
        AllowedTypes = (Space.AllowedDiscussionTypes.Count > 0
            ? Space.AllowedDiscussionTypes.ToList()
            : [0, 1, 2, 3, 4, 5, 6, 7, 8])
            .Where(t => t != 3)
            .ToList();

        // If only one type allowed, redirect directly to that type's page
        if (AllowedTypes.Count == 1)
        {
            var slug = TypeSlugFromInt(AllowedTypes[0]);
            return Redirect($"{SnakkUrlHelper.NewDiscussion(CommunityContext, hubSlug, spaceSlug)}/{slug}");
        }

        return Page();
    }

    public static string TypeSlugFromInt(int type) => type switch
    {
        0 => "standard",
        1 => "question",
        2 => "poll",
        3 => "banner",
        4 => "link",
        5 => "gallery",
        6 => "guide",
        7 => "debate",
        8 => "journal",
        _ => "standard"
    };

    public static (string Description, string Features) TypeInfo(int type) => type switch
    {
        0 => ("Start a conversation, share thoughts, or discuss a topic with the community. The classic forum thread.",
              "Replies in chronological order, rich text formatting, reactions"),
        1 => ("Ask the community a question and get answers. The best answer can be marked as the accepted solution.",
              "Accepted answer highlighting, mark as solved, structured sections"),
        2 => ("Put a question to a vote. Add options and see how the community feels in real time.",
              "Single or multi-vote, optional end date, anonymous voting, live results"),
        3 => ("Post an official banner from the moderation team. Banners are highlighted and can be pinned.",
              "Mod/admin only, pinnable, highlighted styling"),
        4 => ("Share an interesting link with the community. Add your thoughts and start a discussion around it.",
              "URL with preview, commentary, link metadata"),
        5 => ("Share a collection of images with descriptions. Perfect for showcasing work, photography, or visual content.",
              "Image uploads, captions, gallery layout"),
        6 => ("Write a structured how-to guide or tutorial. Help others learn with step-by-step instructions.",
              "Summary, prerequisites, numbered steps, semi-evergreen content"),
        7 => ("Pose a topic and let the community argue different sides. Each reply takes a position.",
              "2-3 defined positions, colored labels, filter by side, optional neutral stance"),
        8 => ("A living thread where the author posts timestamped updates over time. Others can reply and discuss.",
              "Append-only updates, progress tracking, changelogs, project logs"),
        _ => ("", "")
    };
}
