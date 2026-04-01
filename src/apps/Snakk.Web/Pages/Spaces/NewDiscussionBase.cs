using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Helpers;
using Snakk.Web.Services;
using Snakk.Protos.Hub;
using Snakk.Protos.Space;
using Snakk.Protos.Community;

namespace Snakk.Web.Pages.Spaces;

/// <summary>
/// Shared base for all "new discussion" type-specific pages.
/// All pages use /new/{type}?spaceId={publicId} routing.
/// Resolves space, hub, and community from the space's public ID.
/// </summary>
public abstract class NewDiscussionBaseModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    protected readonly SnakkApiClient ApiClient = apiClient;

    public SpaceInfo? Space { get; set; }
    public HubInfo? Hub { get; set; }
    public CommunityInfo? CommunityDetail { get; set; }
    public string HubSlug { get; set; } = string.Empty;
    public string SpaceSlug { get; set; } = string.Empty;
    public string CommunitySlug { get; set; } = string.Empty;

    [BindProperty] public string? NewTitle { get; set; }
    [BindProperty] public string? NewContent { get; set; }
    [BindProperty] public string? SpaceId { get; set; }
    public string? CreateError { get; set; }

    /// <summary>The DiscussionTypeEnum integer value for this page.</summary>
    protected abstract int DiscussionType { get; }

    /// <summary>URL slug for this type (e.g. "standard", "question", "poll").</summary>
    protected abstract string TypeSlug { get; }

    public async Task<IActionResult> OnGetAsync([FromQuery] string spaceId)
    {
        SpaceId = spaceId;

        if (string.IsNullOrEmpty(spaceId))
            return Redirect("/new");

        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
        {
            var returnUrl = BuildReturnUrl();
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        return await LoadPageData();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        SpaceId ??= string.Empty;

        if (string.IsNullOrEmpty(SpaceId))
            return Redirect("/new");

        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
        {
            var returnUrl = BuildReturnUrl();
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        if (string.IsNullOrWhiteSpace(NewTitle) || string.IsNullOrWhiteSpace(NewContent))
        {
            CreateError = "Title and content are required.";
            return await LoadPageData();
        }

        // Load space for creation
        var loadResult = await LoadPageData();
        if (Space is null) return loadResult;

        var result = await CreateDiscussionAsync();

        if (result is null)
        {
            CreateError = "Failed to create discussion. Please try again.";
            return Page();
        }

        var discussionUrl = SnakkUrlHelper.Discussion(
            CommunityContext,
            HubSlug,
            SpaceSlug,
            SnakkUrlHelper.DiscussionSlugId(result.Slug, result.PublicId));

        return Redirect(discussionUrl);
    }

    /// <summary>
    /// Override to pass type-specific parameters to CreateDiscussionAsync.
    /// Base implementation creates a standard discussion.
    /// </summary>
    protected virtual async Task<Snakk.Protos.Discussion.DiscussionCreatedInfo?> CreateDiscussionAsync()
        => await ApiClient.CreateDiscussionAsync(
            Space!.PublicId,
            NewTitle!.Trim(),
            NewContent!,
            DiscussionType);

    private string BuildReturnUrl()
        => $"/new/{TypeSlug}?spaceId={Uri.EscapeDataString(SpaceId ?? "")}";

    private async Task<IActionResult> LoadPageData()
    {
        // Look up space by public ID — derives hub/community slugs from the response
        if (Space is null)
        {
            Space = await ApiClient.GetSpaceAsync(SpaceId!);
            if (Space is null)
                return NotFound();
        }

        SpaceSlug = Space.Slug;
        HubSlug = Space.HubSlug;
        CommunitySlug = Space.CommunitySlug;

        // Set community context so URL helpers work
        if (!string.IsNullOrEmpty(CommunitySlug))
            CommunityContext.SetCommunity(CommunitySlug);

        var hubTask = !string.IsNullOrEmpty(HubSlug) && !string.IsNullOrEmpty(CommunitySlug)
            ? ApiClient.GetHubBySlugResultAsync(HubSlug, CommunitySlug)
            : Task.FromResult(GrpcResult<HubInfo>.NotFound());
        var communityTask = !string.IsNullOrEmpty(CommunitySlug)
            ? ApiClient.GetCommunityBySlugAsync(CommunitySlug)
            : Task.FromResult<CommunityInfo?>(null);

        await Task.WhenAll(hubTask, communityTask);

        Hub = hubTask.Result.IsSuccess ? hubTask.Result.Value : null;
        CommunityDetail = communityTask.IsCompletedSuccessfully ? communityTask.Result : null;

        // Group access check
        if (CommunityDetail is not null
            && (Space.IsRestricted || Hub?.IsRestricted == true || CommunityDetail.IsRestricted))
        {
            var access = await ApiClient.CheckGroupAccessAsync(
                CommunityDetail.PublicId,
                Hub?.PublicId,
                Space.PublicId);

            if (access is not null && !access.CanRead)
                return StatusCode(403);
        }

        return Page();
    }
}
