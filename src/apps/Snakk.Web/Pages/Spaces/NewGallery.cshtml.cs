using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class NewGalleryModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : NewDiscussionBaseModel(apiClient, configuration, communityContext)
{
    protected override int DiscussionType => 5;
    protected override string TypeSlug => "images";

    [BindProperty] public List<string> ImagesImageUrls { get; set; } = [];
    [BindProperty] public string ImagesLayout { get; set; } = "masonry";

    protected override async Task<Snakk.Protos.Discussion.DiscussionCreatedInfo?> CreateDiscussionAsync()
    {
        var imageUrls = ImagesImageUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? [];
        var content = NewContent?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            content = " ";

        return await ApiClient.CreateDiscussionAsync(
            Space!.PublicId,
            NewTitle!.Trim(),
            content,
            DiscussionType,
            imagesLayout: ImagesLayout,
            imagesImageUrls: imageUrls);
    }
}
