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
    [BindProperty] public string GalleryLayout { get; set; } = "grid";

    protected override async Task<Snakk.Protos.Discussion.DiscussionCreatedInfo?> CreateDiscussionAsync()
    {
        var imageUrls = GalleryImageUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? [];
        var content = NewContent?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            content = " ";

        return await ApiClient.CreateDiscussionAsync(
            Space!.PublicId,
            NewTitle!.Trim(),
            content,
            DiscussionType,
            galleryLayout: GalleryLayout,
            galleryImageUrls: imageUrls);
    }
}
