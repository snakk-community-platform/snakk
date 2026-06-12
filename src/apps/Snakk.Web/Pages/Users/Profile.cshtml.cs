using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Protos.Discussion;
using Snakk.Shared.Helpers;
using Snakk.Web.Services;
using Snakk.Protos.User;
using System.Text.Json;

namespace Snakk.Web.Pages.Users;

public record ProfileSocialLink(string Platform, string DisplayName, string Username, string? Url);

[OutputCache(PolicyName = "AnonymousProfile")]
public class ProfileModel(
    SnakkApiClient apiClient,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public UserProfileInfo? Profile { get; set; }
    public PagedRecentDiscussionList? RecentDiscussions { get; set; }
    public List<RecentDiscussionInfo> TopDiscussionsFull { get; set; } = [];
    public List<ProfileSocialLink>? SocialLinks { get; set; }
    public bool IsMessagingEnabled { get; private set; }
    public string ActiveTab { get; set; } = "discussions";

    public string FormatDate(DateTimeOffset? dateTime)
    {
        if (!dateTime.HasValue) return "Unknown";
        return FormatRelativeTime(dateTime.Value);
    }

    public async Task<IActionResult> OnGetAsync(string publicId, string? tab, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string decodedPublicId;
        try { decodedPublicId = UlidBase62.Decode(publicId); }
        catch { return NotFound(); }

        var profileResult = await _apiClient.GetUserProfileResultAsync(decodedPublicId);

        if (!profileResult.IsSuccess)
            return profileResult.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        Profile = profileResult.Value;
        ActiveTab = tab is "top" or "posts" ? tab : "discussions";
        IsMessagingEnabled = Configuration.GetValue<bool>("Features:PrivateMessagingEnabled");

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, _apiClient);

        // Fetch recent discussions, top discussions, and social links in parallel
        var discussionsTask = FetchDiscussionsAsync(decodedPublicId, viewerAllowsAdult);
        var topDiscussionsTask = FetchTopDiscussionsFullAsync(Profile!);
        var socialLinksTask = FetchSocialLinksAsync(decodedPublicId);

        await Task.WhenAll(discussionsTask, topDiscussionsTask, socialLinksTask);

        RecentDiscussions = discussionsTask.Result;
        TopDiscussionsFull = topDiscussionsTask.Result;
        SocialLinks = socialLinksTask.Result;

        return Page();
    }

    private async Task<PagedRecentDiscussionList?> FetchDiscussionsAsync(string decodedPublicId, bool viewerAllowsAdult)
    {
        try
        {
            return await _apiClient.GetRecentDiscussionsAsync(
                offset: 0, pageSize: 5, authorId: decodedPublicId, viewerAllowsAdult: viewerAllowsAdult);
        }
        catch { return null; }
    }

    private async Task<List<RecentDiscussionInfo>> FetchTopDiscussionsFullAsync(UserProfileInfo profile)
    {
        try
        {
            var ids = profile.TopDiscussions.Select(t => t.PublicId).ToList();
            if (ids.Count == 0) return [];
            var result = await _apiClient.GetRecentDiscussionsByIdsAsync(ids);
            if (result?.Items == null || result.Items.Count == 0) return [];
            var order = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            return [.. result.Items.OrderBy(d => order.TryGetValue(d.PublicId, out var i) ? i : int.MaxValue)];
        }
        catch { return []; }
    }

    private async Task<List<ProfileSocialLink>?> FetchSocialLinksAsync(string decodedPublicId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("InternalApi");
            var response = await client.GetAsync($"/users/{Uri.EscapeDataString(decodedPublicId)}/social");
            if (!response.IsSuccessStatusCode) return null;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (!doc.RootElement.TryGetProperty("links", out var linksEl)) return null;

            return linksEl.EnumerateArray().Select(l => new ProfileSocialLink(
                l.GetProperty("platform").GetString() ?? "",
                l.GetProperty("displayName").GetString() ?? "",
                l.GetProperty("username").GetString() ?? "",
                l.TryGetProperty("url", out var urlEl) && urlEl.ValueKind != JsonValueKind.Null
                    ? urlEl.GetString()
                    : null
            )).ToList();
        }
        catch { return null; }
    }
}
