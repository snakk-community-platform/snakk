using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class NewGalleryModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    DiscussionCreateRateLimiter rateLimiter) : CreateDiscussionBaseModel(apiClient, configuration, communityContext, rateLimiter)
{
    protected override int DiscussionType => 5;
    protected override string TypeSlug => "images";
    protected override void PreloadPageCss() { base.PreloadPageCss(); Preload("type-images"); }

    [BindProperty] public List<string> ImagesImageUrls { get; set; } = [];
    [BindProperty] public string ImagesLayout { get; set; } = "masonry";
    [BindProperty] public bool ImagesIsSpoiler { get; set; }

    protected override async Task<Snakk.Protos.Discussion.DiscussionCreatedInfo?> CreateDiscussionAsync()
    {
        var imageUrls = ImagesImageUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? [];

        if (imageUrls.Count == 0)
        {
            CreateError = "At least one image is required.";
            return null;
        }
        if (imageUrls.Count > 20)
        {
            CreateError = "Gallery discussions support a maximum of 20 images.";
            return null;
        }

        return await ApiClient.CreateDiscussionAsync(
            Space!.PublicId,
            NewTitle!.Trim(),
            NewContent!.Trim(),
            DiscussionType,
            imagesLayout: ImagesLayout,
            imagesImageUrls: imageUrls,
            imagesIsSpoiler: ImagesIsSpoiler);
    }
}
