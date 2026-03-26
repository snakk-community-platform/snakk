using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Helpers;
using Snakk.Web.Services;
using Snakk.Protos.Hub;
using Snakk.Protos.Space;
using Snakk.Protos.Community;
using Snakk.Shared.Enums;

namespace Snakk.Web.Pages.Spaces;

/// <summary>
/// Shared base for all "new discussion" type-specific pages.
/// Handles auth, space/hub loading, group access, and discussion creation.
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

    [BindProperty] public string? NewTitle { get; set; }
    [BindProperty] public string? NewContent { get; set; }
    public string? CreateError { get; set; }

    /// <summary>The DiscussionTypeEnum integer value for this page.</summary>
    protected abstract int DiscussionType { get; }

    public async Task<IActionResult> OnGetAsync(string hubSlug, string spaceSlug)
    {
        HubSlug = hubSlug;
        SpaceSlug = spaceSlug;

        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
        {
            var returnUrl = $"{SnakkUrlHelper.NewDiscussion(CommunityContext, hubSlug, spaceSlug)}/{TypeSlug}";
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        return await LoadPageData(hubSlug, spaceSlug);
    }

    public async Task<IActionResult> OnPostAsync(string hubSlug, string spaceSlug)
    {
        HubSlug = hubSlug;
        SpaceSlug = spaceSlug;

        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
        {
            var returnUrl = $"{SnakkUrlHelper.NewDiscussion(CommunityContext, hubSlug, spaceSlug)}/{TypeSlug}";
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        if (string.IsNullOrWhiteSpace(NewTitle) || string.IsNullOrWhiteSpace(NewContent))
        {
            CreateError = "Title and content are required.";
            return await LoadPageData(hubSlug, spaceSlug);
        }

        var spaceResult = await ApiClient.GetSpaceBySlugResultAsync(spaceSlug, hubSlug);
        if (!spaceResult.IsSuccess || spaceResult.Value is null)
            return NotFound();

        Space = spaceResult.Value;

        var result = await CreateDiscussionAsync();

        if (result is null)
        {
            CreateError = "Failed to create discussion. Please try again.";
            return await LoadPageData(hubSlug, spaceSlug);
        }

        var discussionUrl = SnakkUrlHelper.Discussion(
            CommunityContext,
            hubSlug,
            spaceSlug,
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

    /// <summary>URL slug for this type (e.g. "standard", "question", "poll").</summary>
    protected abstract string TypeSlug { get; }

    private async Task<IActionResult> LoadPageData(string hubSlug, string spaceSlug)
    {
        var hubTask = ApiClient.GetHubBySlugResultAsync(hubSlug, CommunityContext.CommunitySlug!);
        var spaceTask = Space is not null
            ? Task.FromResult(GrpcResult<SpaceInfo>.Ok(Space))
            : ApiClient.GetSpaceBySlugResultAsync(spaceSlug, hubSlug);
        var communityTask = !string.IsNullOrEmpty(CommunityContext.CommunitySlug)
            ? ApiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug)
            : Task.FromResult<CommunityInfo?>(null);

        await Task.WhenAll(hubTask, spaceTask, communityTask);

        Hub = hubTask.Result.IsSuccess ? hubTask.Result.Value : null;
        CommunityDetail = communityTask.IsCompletedSuccessfully ? communityTask.Result : null;

        if (!spaceTask.Result.IsSuccess)
            return spaceTask.Result.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        Space = spaceTask.Result.Value!;

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
