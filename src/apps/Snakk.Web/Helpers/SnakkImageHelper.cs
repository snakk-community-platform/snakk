namespace Snakk.Web.Helpers;

using Snakk.Protos.Discussion;

public static class SnakkImageHelper
{
    // Matches the four-tier layout (see _layout.scss):
    //   ≥1540px: center column is 54rem (864px)
    //   1024–1539.98px: center fills remaining space after the 16rem left column
    //   <1024px: center spans the full viewport
    public const string DefaultSizes =
        "(min-width: 1540px) 864px, (min-width: 1024px) calc(100vw - 16rem), 100vw";

    public static string? Srcset(ImagesPreviewItem img)
    {
        var parts = new List<string>();

        if (img.HasThumbnailUrl && img.HasThumbnailWidth)
            parts.Add($"{img.ThumbnailUrl} {img.ThumbnailWidth}w");

        if (img.HasMediumThumbnailUrl && img.HasMediumThumbnailWidth)
            parts.Add($"{img.MediumThumbnailUrl} {img.MediumThumbnailWidth}w");

        if (img.Width > 0)
            parts.Add($"{img.Url} {img.Width}w");

        return parts.Count > 1 ? string.Join(", ", parts) : null;
    }

    public static string? Srcset(ImagesImageProto img)
    {
        var parts = new List<string>();

        if (img.HasThumbnailUrl && img.HasThumbnailWidth)
            parts.Add($"{img.ThumbnailUrl} {img.ThumbnailWidth}w");

        if (img.HasMediumThumbnailUrl && img.HasMediumThumbnailWidth)
            parts.Add($"{img.MediumThumbnailUrl} {img.MediumThumbnailWidth}w");

        if (img.Width > 0)
            parts.Add($"{img.Url} {img.Width}w");

        return parts.Count > 1 ? string.Join(", ", parts) : null;
    }
}
