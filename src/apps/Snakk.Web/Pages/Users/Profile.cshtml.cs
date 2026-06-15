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
public record ProfileModRole(string Role, string EntityType, string? EntityId, string? EntityName, string? AccessLevel);

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
    public List<ProfileModRole> ModerationRoles { get; set; } = [];
    public bool IsMessagingEnabled { get; private set; }
    public string ActiveTab { get; set; } = "discussions";

    public string FormatDate(DateTimeOffset? dateTime)
    {
        if (!dateTime.HasValue) return "Unknown";
        return FormatRelativeTime(dateTime.Value);
    }

    public async Task<IActionResult> OnGetAsync(string slug, string? tab, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var profileResult = await _apiClient.GetUserBySlugResultAsync(slug, cancellationToken);

        if (!profileResult.IsSuccess)
            return profileResult.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        Profile = profileResult.Value;
        ActiveTab = tab is "top" or "posts" ? tab : "discussions";
        IsMessagingEnabled = Configuration.GetValue<bool>("Features:PrivateMessagingEnabled");

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, _apiClient);

        var publicId = Profile!.PublicId;
        var discussionsTask = FetchDiscussionsAsync(publicId, viewerAllowsAdult);
        var topDiscussionsTask = FetchTopDiscussionsFullAsync(Profile);
        var socialLinksTask = FetchSocialLinksAsync(publicId);
        var modRolesTask = FetchModerationRolesAsync(publicId);

        await Task.WhenAll(discussionsTask, topDiscussionsTask, socialLinksTask, modRolesTask);

        RecentDiscussions = discussionsTask.Result;
        TopDiscussionsFull = topDiscussionsTask.Result;
        SocialLinks = socialLinksTask.Result;
        ModerationRoles = modRolesTask.Result;

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

    private async Task<List<ProfileModRole>> FetchModerationRolesAsync(string decodedPublicId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("InternalApi");
            var response = await client.GetAsync($"/users/{Uri.EscapeDataString(decodedPublicId)}/mod-roles");
            if (!response.IsSuccessStatusCode) return [];

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (!doc.RootElement.TryGetProperty("items", out var itemsEl)) return [];

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return itemsEl.Deserialize<List<ProfileModRole>>(options) ?? [];
        }
        catch { return []; }
    }
}
