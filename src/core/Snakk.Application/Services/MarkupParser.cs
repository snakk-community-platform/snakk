namespace Snakk.Application.Services;

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

/// <summary>
/// Markdown parser powered by Markdig with GFM extensions.
/// Produces sanitized HTML — raw HTML in markdown input is escaped via DisableHtml().
/// Links are validated to only allow http, https, and mailto schemes.
/// </summary>
public class MarkupParser : IMarkupParser
{
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "mailto"
    };

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Use(new SpoilerExtension())
        .Use(new EntityLinkExtension())
        .Build();

    private static readonly MarkdownPipeline PlainTextPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string ToHtml(string markup) => ToHtml(markup, autoParagraph: false, null);

    public string ToHtml(string markup, bool autoParagraph) => ToHtml(markup, autoParagraph, null);

    public string ToHtml(string markup, bool autoParagraph, IReadOnlyDictionary<string, ImageRenderData>? imageData)
    {
        if (string.IsNullOrEmpty(markup))
            return string.Empty;

        if (autoParagraph)
            markup = ParagraphSplitter.Split(markup);

        var document = Markdown.Parse(markup, Pipeline);

        SanitizeLinks(document);
        CleanEmptyTables(document);
        NormalizeCodeBlockLanguages(document);
        TrimCodeBlocks(document);

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();

        var html = writer.ToString().Trim();

        // Wrap tables in a scrollable container
        html = WrapTablesInScrollContainer(html);

        // Replace image-group fenced blocks with gallery HTML (uses imageData for srcset + blur)
        html = ReplaceImageGroups(html, imageData);

        // Enrich standalone <img> tags from plain markdown with srcset + data-blur
        html = TransformStandaloneImages(html, imageData);

        return html;
    }

    public string ToPlainText(string markup)
    {
        if (string.IsNullOrEmpty(markup))
            return string.Empty;

        return Markdown.ToPlainText(markup, PlainTextPipeline).Trim();
    }

    /// <summary>
    /// Checks if raw markup content contains code blocks or inline code.
    /// </summary>
    public static bool ContainsCode(string content) =>
        !string.IsNullOrEmpty(content) && content.Contains('`');

    private static void SanitizeLinks(MarkdownDocument document)
    {
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!IsValidUrl(link.Url))
            {
                link.Url = "";
            }
            else if (IsInternalPath(link.Url))
            {
                // Internal links get entity-link styling, no target="_blank"
                link.GetAttributes().AddClass("entity-link");

                // For auto-linked full URLs, rewrite href and text to just the path
                if (Uri.TryCreate(link.Url, UriKind.Absolute, out var internalUri)
                    && (internalUri.Scheme == "http" || internalUri.Scheme == "https"))
                {
                    var path = internalUri.AbsolutePath;
                    link.Url = path;

                    // Replace the auto-linked text (full URL) with just the path
                    if (link.FirstChild is LiteralInline literal
                        && literal.Content.ToString().StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        literal.Content = new Markdig.Helpers.StringSlice(path);
                    }
                }
            }
            else
            {
                link.GetAttributes().AddProperty("target", "_blank");
                link.GetAttributes().AddProperty("rel", "nofollow noopener noreferrer");
            }
        }
    }

    private static void CleanEmptyTables(MarkdownDocument document)
    {
        var tables = document.Descendants<Table>().ToList();

        foreach (var table in tables)
        {
            var rows = table.OfType<TableRow>().ToList();
            if (rows.Count == 0)
            {
                table.Parent?.Remove(table);
                continue;
            }

            var colCount = rows.Max(r => r.Count);
            if (colCount == 0)
            {
                table.Parent?.Remove(table);
                continue;
            }

            // Identify non-empty columns (any cell in that column has content)
            var nonEmptyCols = new HashSet<int>();
            foreach (var row in rows)
            {
                for (var c = 0; c < row.Count; c++)
                {
                    if (row[c] is TableCell cell && CellHasContent(cell))
                        nonEmptyCols.Add(c);
                }
            }

            // If all columns are empty, remove the entire table
            if (nonEmptyCols.Count == 0)
            {
                table.Parent?.Remove(table);
                continue;
            }

            // Remove entirely empty data rows (keep header rows)
            var emptyDataRows = rows
                .Where(r => !r.IsHeader && !RowHasContent(r, nonEmptyCols))
                .ToList();

            foreach (var row in emptyDataRows)
                table.Remove(row);

            // Remove empty columns from remaining rows (iterate in reverse to preserve indices)
            var emptyCols = Enumerable.Range(0, colCount)
                .Where(c => !nonEmptyCols.Contains(c))
                .OrderByDescending(c => c)
                .ToList();

            foreach (var row in table.OfType<TableRow>())
            {
                foreach (var c in emptyCols)
                {
                    if (c < row.Count)
                        row.RemoveAt(c);
                }
            }

            // Also remove empty column definitions
            foreach (var c in emptyCols)
            {
                if (c < table.ColumnDefinitions.Count)
                    table.ColumnDefinitions.RemoveAt(c);
            }

            // A valid table needs at least 2 rows (header + at least 1 data row)
            var remainingRows = table.OfType<TableRow>().ToList();
            if (remainingRows.Count < 2 || !remainingRows.Any(r => !r.IsHeader))
            {
                table.Parent?.Remove(table);
            }
        }
    }

    private static bool CellHasContent(TableCell cell)
    {
        foreach (var paragraph in cell.OfType<ParagraphBlock>())
        {
            if (paragraph.Inline is { } inline)
            {
                foreach (var child in inline)
                {
                    if (child is LiteralInline literal && !literal.Content.IsEmpty)
                        return true;

                    if (child is not LiteralInline)
                        return true;
                }
            }
        }

        return false;
    }

    private static bool RowHasContent(TableRow row, HashSet<int> colsToCheck)
    {
        for (var c = 0; c < row.Count; c++)
        {
            if (colsToCheck.Contains(c) && row[c] is TableCell cell && CellHasContent(cell))
                return true;
        }

        return false;
    }

    private static void TrimCodeBlocks(MarkdownDocument document)
    {
        foreach (var codeBlock in document.Descendants<FencedCodeBlock>())
        {
            if (IsImageGroupBlock(codeBlock))
                continue;

            if (codeBlock.Lines.Count == 0)
                continue;

            // Remove leading empty lines
            while (codeBlock.Lines.Count > 0 && codeBlock.Lines.Lines[0].Slice.IsEmpty)
                codeBlock.Lines.RemoveAt(0);

            // Remove trailing empty lines
            while (codeBlock.Lines.Count > 0 && codeBlock.Lines.Lines[codeBlock.Lines.Count - 1].Slice.IsEmpty)
                codeBlock.Lines.RemoveAt(codeBlock.Lines.Count - 1);
        }
    }

    private static void NormalizeCodeBlockLanguages(MarkdownDocument document)
    {
        foreach (var fenced in document.Descendants<FencedCodeBlock>())
        {
            if (string.IsNullOrEmpty(fenced.Info))
                continue;

            if (IsImageGroupBlock(fenced))
            {
                var layout = ParseImageGroupLayout(fenced.Info);
                var igAttrs = fenced.GetAttributes();
                igAttrs.Classes?.Clear();
                igAttrs.AddProperty("data-image-group", layout);
                continue;
            }

            var lang = fenced.Info.Trim().ToLowerInvariant();
            var attrs = fenced.GetAttributes();
            attrs.Classes?.Clear();
            attrs.AddClass($"language-{lang}");
        }
    }

    private static bool IsImageGroupBlock(FencedCodeBlock block) =>
        block.Info?.TrimStart().StartsWith("image-group", StringComparison.OrdinalIgnoreCase) == true;

    private static string ParseImageGroupLayout(string info)
    {
        foreach (var part in info.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("layout=", StringComparison.OrdinalIgnoreCase))
            {
                return part[7..].ToLowerInvariant() switch
                {
                    "masonry" => "masonry",
                    "justified" => "justified",
                    "carousel" => "carousel",
                    _ => "grid"
                };
            }
        }
        return "grid";
    }

    private static readonly Regex ImageGroupHtmlRegex = new(
        @"<pre><code[^>]*\bdata-image-group=""(\w+)""[^>]*>(.*?)</code></pre>",
        RegexOptions.Singleline | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(500));

    private const string ImageSizes =
        "(min-width: 1540px) 864px, (min-width: 1024px) calc(100vw - 16rem), 100vw";

    // Fallback: derive variant URLs from the full URL when DB data is unavailable.
    private static (string thumb, string med) DeriveVariants(string url)
    {
        if (url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) &&
            url.Contains("/media/posts/"))
        {
            var stem = url[..^5];
            return (stem + "_thumb.webp", stem + "_med.webp");
        }
        return (url, url);
    }

    private static string ImgTag(string src, string alt, ImageRenderData? data = null)
    {
        var (derivedThumb, derivedMed) = DeriveVariants(src);
        var thumb = data?.ThumbUrl ?? derivedThumb;
        var med   = data?.MedUrl   ?? derivedMed;

        // Only include a srcset descriptor when we know the variant exists in the DB.
        // When data is null we fall back to derived URLs and include all three.
        var parts = new List<string>(3);
        if (data is null || data.ThumbUrl is not null) parts.Add($"{thumb} 300w");
        if (data is null || data.MedUrl   is not null) parts.Add($"{med} 900w");
        parts.Add($"{src} 2048w");
        var srcset = string.Join(", ", parts);

        var blurAttr = data?.BlurDataUri is not null ? $" data-blur=\"{data.BlurDataUri}\"" : "";
        var dimAttrs = data?.Width is not null && data.Height is not null
            ? $" width=\"{data.Width}\" height=\"{data.Height}\""
            : "";

        return $"<img src=\"{med}\" srcset=\"{srcset}\" sizes=\"{ImageSizes}\"" +
               $"{blurAttr}{dimAttrs} data-full=\"{src}\" alt=\"{alt}\" loading=\"lazy\" />";
    }

    // Matches standalone <img> tags emitted by Markdig for plain ![alt](url) markdown.
    // Only matches the full-resolution URL (no _thumb/_med suffix) to avoid re-processing
    // images already transformed by ReplaceImageGroups.
    private static readonly Regex StandaloneImgRegex = new(
        @"<img\s[^>]*\bsrc=""([^""]*?/media/posts/\d{4}/\d{2}/\d{2}/[^._/""][^/""]*\.webp)""[^>]*/?>",
        RegexOptions.Compiled | RegexOptions.Singleline,
        TimeSpan.FromMilliseconds(250));

    private static string TransformStandaloneImages(string html, IReadOnlyDictionary<string, ImageRenderData>? imageData)
    {
        if (imageData is null || imageData.Count == 0) return html;
        try
        {
            return StandaloneImgRegex.Replace(html, match =>
            {
                var src = match.Groups[1].Value;
                if (!imageData.TryGetValue(src, out var data)) return match.Value;
                var altMatch = Regex.Match(match.Value, @"\balt=""([^""]*)""");
                var alt = altMatch.Success ? altMatch.Groups[1].Value : "";
                return ImgTag(src, alt, data);
            });
        }
        catch (RegexMatchTimeoutException) { return html; }
    }

    private static string ReplaceImageGroups(string html, IReadOnlyDictionary<string, ImageRenderData>? imageData = null)
    {
        try
        {
            return ImageGroupHtmlRegex.Replace(html, match =>
            {
                var layout = match.Groups[1].Value;
                var rawContent = WebUtility.HtmlDecode(match.Groups[2].Value);

                var lines = rawContent.Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l))
                    .Select(l => {
                        var sep = l.IndexOf('|');
                        var rawSrc = sep >= 0 ? l[..sep].Trim() : l;
                        return (
                            src: WebUtility.HtmlEncode(rawSrc),
                            alt: sep >= 0 ? WebUtility.HtmlEncode(l[(sep + 1)..].Trim()) : "",
                            rawSrc
                        );
                    })
                    .Where(x => !string.IsNullOrEmpty(x.src))
                    .ToList();

                static string ItemDiv(string src, string alt, string rawSrc, IReadOnlyDictionary<string, ImageRenderData>? data)
                {
                    var imgData = data is not null && data.TryGetValue(rawSrc, out var d) ? d : null;
                    var blurAttr = imgData?.BlurDataUri is not null ? $" data-blur=\"{imgData.BlurDataUri}\"" : "";
                    return $"<div class=\"images-upload-item\" data-full=\"{src}\"{blurAttr}>{ImgTag(src, alt, imgData)}</div>";
                }

                if (layout == "carousel")
                {
                    var sb = new StringBuilder("<div class=\"gup-carousel ig-carousel\">");
                    sb.Append("<div class=\"gup-carousel-track\">");
                    foreach (var (src, alt, rawSrc) in lines)
                        sb.Append(ItemDiv(src, alt, rawSrc, imageData));
                    sb.Append("</div>");
                    if (lines.Count > 1)
                    {
                        sb.Append("<button type=\"button\" class=\"gup-carousel-arrow gup-carousel-prev\" aria-label=\"Previous\">&#8249;</button>");
                        sb.Append("<button type=\"button\" class=\"gup-carousel-arrow gup-carousel-next\" aria-label=\"Next\">&#8250;</button>");
                        sb.Append($"<span class=\"gup-carousel-counter\">1 / {lines.Count}</span>");
                    }
                    sb.Append("</div>");
                    return sb.ToString();
                }

                var containerClass = layout switch
                {
                    "masonry" => "gup-masonry",
                    "justified" => "gup-justified",
                    _ => "gup-grid"
                };

                var sbGrid = new StringBuilder($"<div class=\"{containerClass}\">");
                foreach (var (src, alt, rawSrc) in lines)
                    sbGrid.Append(ItemDiv(src, alt, rawSrc, imageData));
                sbGrid.Append("</div>");
                return sbGrid.ToString();
            });
        }
        catch (RegexMatchTimeoutException)
        {
            return html;
        }
    }

    private static readonly Regex TableRegex = new(
        @"<table\b[^>]*>.*?</table>",
        RegexOptions.Singleline | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    private static string WrapTablesInScrollContainer(string html)
    {
        try
        {
            return TableRegex.Replace(html, match => $"<div class=\"table-scroll\">{match.Value}</div>");
        }
        catch (RegexMatchTimeoutException)
        {
            return html;
        }
    }

    private static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        // Allow relative URLs starting with / (but not protocol-relative //)
        // Must check before Uri.TryCreate because Unix treats /path as file:// URI
        if (url.StartsWith('/') && !url.StartsWith("//"))
            return true;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return AllowedSchemes.Contains(uri.Scheme);

        return false;
    }

    /// <summary>
    /// Checks whether a URL points to an internal entity path (/c/..., /u/...).
    /// Handles both relative paths and absolute URLs with a domain.
    /// </summary>
    private static bool IsInternalPath(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var path = url;

        // Extract path from absolute URLs
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            path = uri.AbsolutePath;
        }

        return InternalPathRegex.IsMatch(path);
    }

    // Matches internal entity paths: /c/{slug}/h/{hub}, /c/{slug}/h/{hub}/{space},
    // /c/{slug}/h/{hub}/{space}/{slug}~{id}, and /u/{id}
    private static readonly Regex InternalPathRegex = new(
        @"^(/c/[\w-]+(/h/[\w-]+(/([\w-]+(/([\w-]+~[\w]+))?)?)?)?|/u/[\w]+)$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    // Matches bare internal paths or full URLs containing internal paths in plain text.
    // Used by the EntityLinkParser to detect unlinked references.
    private static readonly Regex EntityLinkPatternRegex = new(
        @"(https?://[^\s/]+)?(/c/[\w-]+(/h/[\w-]+(/([\w-]+(/([\w-]+~[\w]+))?)?)?)?)|(https?://[^\s/]+)?(/u/[\w]+)",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    // =========================================================================
    // Spoiler extension — renders ||text|| as <span class="spoiler">text</span>
    // =========================================================================

    private sealed class SpoilerInline : ContainerInline { }

    private sealed class SpoilerParser : InlineParser
    {
        public SpoilerParser()
        {
            OpeningCharacters = ['|'];
        }

        public override bool Match(InlineProcessor processor, ref StringSlice slice)
        {
            if (slice.CurrentChar != '|' || slice.PeekChar(1) != '|')
                return false;

            var text = slice.Text;
            var contentStart = slice.Start + 2;
            var closeIdx = text.IndexOf("||", contentStart, StringComparison.Ordinal);

            if (closeIdx < 0 || closeIdx == contentStart)
                return false;

            var content = text.Substring(contentStart, closeIdx - contentStart);

            if (content.Contains('\n') || content.Contains('\r'))
                return false;

            var inline = new SpoilerInline();
            inline.AppendChild(new LiteralInline(content));
            processor.Inline = inline;
            slice.Start = closeIdx + 2;
            return true;
        }
    }

    private sealed class SpoilerRenderer : HtmlObjectRenderer<SpoilerInline>
    {
        protected override void Write(HtmlRenderer renderer, SpoilerInline obj)
        {
            renderer.Write("<span class=\"spoiler\">");
            renderer.WriteChildren(obj);
            renderer.Write("</span>");
        }
    }

    private sealed class SpoilerExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline) =>
            pipeline.InlineParsers.AddIfNotAlready<SpoilerParser>();

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is HtmlRenderer htmlRenderer)
                htmlRenderer.ObjectRenderers.AddIfNotAlready<SpoilerRenderer>();
        }
    }

    // =========================================================================
    // Entity link extension — auto-links internal paths and URLs
    // Detects bare /c/... /u/... paths and full http(s):// URLs pointing to them
    // =========================================================================

    private sealed class EntityLinkInline : LeafInline
    {
        /// <summary>
        /// The internal path (always relative, e.g. /c/gaming/h/fps).
        /// </summary>
        public string Path { get; set; } = "";
    }

    private sealed class EntityLinkParser : InlineParser
    {
        public EntityLinkParser()
        {
            // Trigger on '/' (bare paths) and 'h' (http/https URLs)
            OpeningCharacters = ['/', 'h'];
        }

        public override bool Match(InlineProcessor processor, ref StringSlice slice)
        {
            var start = slice.Start;
            var text = slice.Text;

            // Don't match if preceded by an alphanumeric char (we want word boundaries)
            if (start > 0 && char.IsLetterOrDigit(text[start - 1]))
                return false;

            // Extract remaining text from current position to end of line or whitespace
            var remaining = text.AsSpan(start);
            var endIdx = 0;
            while (endIdx < remaining.Length && !char.IsWhiteSpace(remaining[endIdx]))
                endIdx++;

            if (endIdx == 0)
                return false;

            var candidate = remaining[..endIdx].ToString();

            // Try to match against entity link pattern
            var match = EntityLinkPatternRegex.Match(candidate);
            if (!match.Success || match.Index != 0)
                return false;

            var fullMatch = match.Value;
            string path;

            // Extract the path portion
            if (fullMatch.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                // Full URL — extract path from URI
                if (!Uri.TryCreate(fullMatch, UriKind.Absolute, out var uri))
                    return false;

                path = uri.AbsolutePath;
            }
            else
            {
                path = fullMatch;
            }

            // Validate it's a real internal path
            if (!InternalPathRegex.IsMatch(path))
                return false;

            processor.Inline = new EntityLinkInline { Path = path };
            slice.Start = start + fullMatch.Length;
            return true;
        }
    }

    private sealed class EntityLinkRenderer : HtmlObjectRenderer<EntityLinkInline>
    {
        protected override void Write(HtmlRenderer renderer, EntityLinkInline obj)
        {
            renderer.Write("<a href=\"");
            renderer.WriteEscapeUrl(obj.Path);
            renderer.Write("\" class=\"entity-link\">");
            renderer.WriteEscape(obj.Path);
            renderer.Write("</a>");
        }
    }

    private sealed class EntityLinkExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline) =>
            pipeline.InlineParsers.AddIfNotAlready<EntityLinkParser>();

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is HtmlRenderer htmlRenderer)
                htmlRenderer.ObjectRenderers.AddIfNotAlready<EntityLinkRenderer>();
        }
    }
}
