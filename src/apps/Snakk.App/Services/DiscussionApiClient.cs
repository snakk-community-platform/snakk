namespace Snakk.App.Services;

using Snakk.App.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public class DiscussionApiClient(HttpClient http, AuthService auth)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<PagedResponse<DiscussionListItem>> GetRecentAsync(int limit = 20, string? cursor = null)
    {
        var token = await auth.GetTokenAsync();
        if (token is not null)
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var url = $"/api/v1/discussions/recent?limit={limit}";
        if (cursor is not null) url += $"&cursor={Uri.EscapeDataString(cursor)}";

        var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<DiscussionListItem>>(JsonOptions)
                    ?? new PagedResponse<DiscussionListItem>([], false, null);

        return ResolveUrls(paged);
    }

    private PagedResponse<DiscussionListItem> ResolveUrls(PagedResponse<DiscussionListItem> paged)
    {
        var baseUrl = http.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;
        if (string.IsNullOrEmpty(baseUrl)) return paged;

        return paged with
        {
            Items = paged.Items.Select(d => d with
            {
                Author = ResolveAuthor(d.Author, baseUrl),
                LastReplier = ResolveAuthor(d.LastReplier, baseUrl),
                Images = d.Images is null ? null : d.Images with
                {
                    Items = d.Images.Items.Select(i => i with
                    {
                        Url = Abs(i.Url, baseUrl)!,
                        ThumbnailUrl = Abs(i.ThumbnailUrl, baseUrl),
                        MediumThumbnailUrl = Abs(i.MediumThumbnailUrl, baseUrl),
                    }).ToList()
                }
            }).ToList()
        };
    }

    private static AuthorSummary? ResolveAuthor(AuthorSummary? author, string baseUrl) =>
        author is null ? null : author with
        {
            AvatarUrl = Abs(author.AvatarUrl, baseUrl),
            AvatarMicroUrl = Abs(author.AvatarMicroUrl, baseUrl),
        };

    private static string? Abs(string? url, string baseUrl) =>
        url is null ? null : url.StartsWith('/') ? $"{baseUrl}{url}" : url;
}
