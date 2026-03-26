using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class NewGalleryModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : NewDiscussionBaseModel(apiClient, configuration, communityContext)
{
    protected override int DiscussionType => 5;
    protected override string TypeSlug => "gallery";

    [BindProperty] public List<string> GalleryImageUrls { get; set; } = [];

    protected override async Task<Snakk.Protos.Discussion.DiscussionCreatedInfo?> CreateDiscussionAsync()
    {
        // Prepend gallery images as markdown to the content
        var imageUrls = GalleryImageUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? [];
        var imageMarkdown = string.Join("\n\n", imageUrls.Select(url => $"![gallery image]({url})"));

        var fullContent = !string.IsNullOrWhiteSpace(NewContent)
            ? $"{imageMarkdown}\n\n{NewContent}"
            : imageMarkdown;

        if (string.IsNullOrWhiteSpace(fullContent))
            fullContent = " "; // Fallback — at least one image URL should be present

        return await ApiClient.CreateDiscussionAsync(
            Space!.PublicId,
            NewTitle!.Trim(),
            fullContent,
            DiscussionType);
    }
}
