using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Snakk.Protos.Post;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class PostModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IMemoryCache cache) : BasePageModel(configuration, communityContext)
{
    public EnrichedPostInfo? Post { get; set; }
    public bool IsAuthenticated { get; set; }
    public string? CurrentUserId { get; set; }
    public bool IsDiscussionLocked { get; set; }
    public string DiscussionPublicId { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string discussionId, string postId, bool isLocked = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsDiscussionLocked = isLocked;
        DiscussionPublicId = discussionId;

        var user = await apiClient.GetCurrentUserAsync();
        IsAuthenticated = user is not null;
        CurrentUserId = user?.PublicId;

        var cacheKey = $"partial:post:{postId}:{(IsAuthenticated ? "auth" : "anon")}";
        Post = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
            var postNumber = await apiClient.GetPostNumberAsync(discussionId, postId);
            var posts = await apiClient.GetDiscussionPostsAsync(discussionId, offset: postNumber - 1, pageSize: 1);
            return posts?.Items.FirstOrDefault();
        });

        if (Post is null)
            return NotFound();

        return Page();
    }
}
