using Ganss.Xss;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Snakk.Web.Extensions;

/// <summary>
/// Sanitizes server-rendered markdown HTML before writing to the page.
/// Defense-in-depth against any future Markdig bypass — primary sanitization is on the API side.
/// </summary>
public static class HtmlHelperExtensions
{
    private static readonly HtmlSanitizer Sanitizer = BuildSanitizer();

    public static IHtmlContent SanitizedRaw(this IHtmlHelper helper, string? html)
    {
        if (string.IsNullOrEmpty(html))
            return HtmlString.Empty;

        return new HtmlString(Sanitizer.Sanitize(html));
    }

    private static HtmlSanitizer BuildSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        // Allow language class for code blocks (e.g. class="language-csharp")
        sanitizer.AllowedAttributes.Add("class");

        // Allow id for heading anchors
        sanitizer.AllowedAttributes.Add("id");

        // Allow data-* for any Markdig extensions that emit them
        sanitizer.AllowDataAttributes = true;

        // Allow img lazy-loading and decoding hints
        sanitizer.AllowedAttributes.Add("loading");
        sanitizer.AllowedAttributes.Add("decoding");

        // Allow carousel button elements (rendered by ReplaceImageGroups for image-group carousel layout)
        sanitizer.AllowedTags.Add("button");
        sanitizer.AllowedAttributes.Add("type");
        sanitizer.AllowedAttributes.Add("aria-label");

        // Allow canvas for chart blocks (rendered by chart-renderer.ts via Chart.js)
        sanitizer.AllowedTags.Add("canvas");

        // Ensure javascript: and data: hrefs are stripped (already default, made explicit)
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("mailto");

        return sanitizer;
    }
}
