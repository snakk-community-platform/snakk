namespace Snakk.Infrastructure.Rendering;

using System.Net;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Lightweight markup parser supporting:
/// - **bold** and __bold__
/// - *italic* and _italic_
/// - `inline code`
/// - ```code blocks```
/// - [link text](url)
/// - > blockquotes
/// - - unordered lists
/// - 1. ordered lists
/// 
/// All input is HTML-escaped first, preventing XSS attacks.
/// No raw HTML is allowed.
/// </summary>
public partial class MarkupParser : IMarkupParser
{
    // Regex patterns (compiled for performance)
    [GeneratedRegex(@"```([\s\S]*?)```", RegexOptions.Multiline)]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"`([^`\n]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldAsteriskRegex();

    [GeneratedRegex(@"__(.+?)__")]
    private static partial Regex BoldUnderscoreRegex();

    [GeneratedRegex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)")]
    private static partial Regex ItalicAsteriskRegex();

    [GeneratedRegex(@"(?<!_)_(?!_)(.+?)(?<!_)_(?!_)")]
    private static partial Regex ItalicUnderscoreRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"^&gt;\s*(.*)$", RegexOptions.Multiline)]
    private static partial Regex BlockquoteRegex();

    [GeneratedRegex(@"^[-*]\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex UnorderedListItemRegex();

    [GeneratedRegex(@"^\d+\.\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex OrderedListItemRegex();

    // Allowed URL schemes for links
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "mailto"
    };

    public string ToHtml(string markup)
    {
        if (string.IsNullOrEmpty(markup))
            return string.Empty;

        // Step 1: HTML-escape everything first (prevents XSS)
        var html = WebUtility.HtmlEncode(markup);

        // Step 2: Extract and preserve code blocks (they shouldn't be processed further)
        var codeBlocks = new List<string>();
        html = CodeBlockRegex().Replace(html, match =>
        {
            var index = codeBlocks.Count;
            codeBlocks.Add(match.Groups[1].Value.Trim());
            return $"{{{{CODE_BLOCK_{index}}}}}";
        });

        // Step 3: Extract and preserve inline code
        var inlineCodes = new List<string>();
        html = InlineCodeRegex().Replace(html, match =>
        {
            var index = inlineCodes.Count;
            inlineCodes.Add(match.Groups[1].Value);
            return $"{{{{INLINE_CODE_{index}}}}}";
        });

        // Step 4: Apply formatting (bold before italic to handle **bold** vs *italic*)
        html = BoldAsteriskRegex().Replace(html, "<strong>$1</strong>");
        html = BoldUnderscoreRegex().Replace(html, "<strong>$1</strong>");
        html = ItalicAsteriskRegex().Replace(html, "<em>$1</em>");
        html = ItalicUnderscoreRegex().Replace(html, "<em>$1</em>");

        // Step 5: Process links (with URL validation)
        html = LinkRegex().Replace(html, match =>
        {
            var text = match.Groups[1].Value;
            var url = WebUtility.HtmlDecode(match.Groups[2].Value); // Decode the escaped URL

            if (IsValidUrl(url))
            {
                var safeUrl = WebUtility.HtmlEncode(url);
                return $"<a href=\"{safeUrl}\" target=\"_blank\" rel=\"noopener noreferrer\" class=\"link link-primary\">{text}</a>";
            }

            // Invalid URL - just show as text
            return $"[{text}]({WebUtility.HtmlEncode(url)})";
        });

        // Step 6: Process blockquotes
        html = ProcessBlockquotes(html);

        // Step 7: Process lists
        html = ProcessLists(html);

        // Step 8: Restore code blocks
        for (var i = 0; i < codeBlocks.Count; i++)
        {
            html = html.Replace(
                $"{{{{CODE_BLOCK_{i}}}}}",
                $"<pre class=\"bg-base-200 p-3 rounded-lg overflow-x-auto my-2\"><code>{codeBlocks[i]}</code></pre>");
        }

        // Step 9: Restore inline code
        for (var i = 0; i < inlineCodes.Count; i++)
        {
            html = html.Replace(
                $"{{{{INLINE_CODE_{i}}}}}",
                $"<code class=\"bg-base-200 px-1 rounded\">{inlineCodes[i]}</code>");
        }

        // Step 10: Convert line breaks to <br> (except inside pre/code blocks)
        html = ConvertLineBreaks(html);

        return html;
    }

    public string ToPlainText(string markup)
    {
        if (string.IsNullOrEmpty(markup))
            return string.Empty;

        var text = markup;

        // Remove code blocks
        text = CodeBlockRegex().Replace(text, "$1");

        // Remove inline code markers
        text = InlineCodeRegex().Replace(text, "$1");

        // Remove bold markers
        text = BoldAsteriskRegex().Replace(text, "$1");
        text = BoldUnderscoreRegex().Replace(text, "$1");

        // Remove italic markers
        text = Regex.Replace(text, @"\*(.+?)\*", "$1");
        text = Regex.Replace(text, @"_(.+?)_", "$1");

        // Extract link text
        text = LinkRegex().Replace(text, "$1");

        // Remove blockquote markers
        text = Regex.Replace(text, @"^>\s*", "", RegexOptions.Multiline);

        // Remove list markers
        text = Regex.Replace(text, @"^[-*]\s+", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"^\d+\.\s+", "", RegexOptions.Multiline);

        return text.Trim();
    }

    private static bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return AllowedSchemes.Contains(uri.Scheme);
        }

        // Allow relative URLs starting with /
        if (url.StartsWith('/') && !url.StartsWith("//"))
            return true;

        return false;
    }

    private static string ProcessBlockquotes(string html)
    {
        var lines = html.Split('\n');
        var result = new StringBuilder();
        var inBlockquote = false;

        foreach (var line in lines)
        {
            var match = BlockquoteRegex().Match(line);
            if (match.Success)
            {
                if (!inBlockquote)
                {
                    result.Append("<blockquote class=\"border-l-4 border-primary pl-4 my-2 italic text-base-content/80\">");
                    inBlockquote = true;
                }
                result.Append(match.Groups[1].Value);
                result.Append("<br>");
            }
            else
            {
                if (inBlockquote)
                {
                    result.Append("</blockquote>");
                    inBlockquote = false;
                }
                result.Append(line);
                result.Append('\n');
            }
        }

        if (inBlockquote)
        {
            result.Append("</blockquote>");
        }

        return result.ToString().TrimEnd('\n');
    }

    private static string ProcessLists(string html)
    {
        var lines = html.Split('\n');
        var result = new StringBuilder();
        var inUnorderedList = false;
        var inOrderedList = false;

        foreach (var line in lines)
        {
            var ulMatch = UnorderedListItemRegex().Match(line);
            var olMatch = OrderedListItemRegex().Match(line);

            if (ulMatch.Success)
            {
                if (inOrderedList)
                {
                    result.Append("</ol>");
                    inOrderedList = false;
                }
                if (!inUnorderedList)
                {
                    result.Append("<ul class=\"list-disc list-inside my-2\">");
                    inUnorderedList = true;
                }
                result.Append($"<li>{ulMatch.Groups[1].Value}</li>");
            }
            else if (olMatch.Success)
            {
                if (inUnorderedList)
                {
                    result.Append("</ul>");
                    inUnorderedList = false;
                }
                if (!inOrderedList)
                {
                    result.Append("<ol class=\"list-decimal list-inside my-2\">");
                    inOrderedList = true;
                }
                result.Append($"<li>{olMatch.Groups[1].Value}</li>");
            }
            else
            {
                if (inUnorderedList)
                {
                    result.Append("</ul>");
                    inUnorderedList = false;
                }
                if (inOrderedList)
                {
                    result.Append("</ol>");
                    inOrderedList = false;
                }
                result.Append(line);
                result.Append('\n');
            }
        }

        if (inUnorderedList) result.Append("</ul>");
        if (inOrderedList) result.Append("</ol>");

        return result.ToString().TrimEnd('\n');
    }

    private static string ConvertLineBreaks(string html)
    {
        // Don't convert line breaks inside <pre> tags
        var parts = Regex.Split(html, @"(<pre[^>]*>[\s\S]*?</pre>)");
        var result = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.StartsWith("<pre"))
            {
                result.Append(part);
            }
            else
            {
                // Convert double newlines to paragraph breaks, single to <br>
                var processed = Regex.Replace(part, @"\n\n+", "</p><p class=\"my-2\">");
                processed = processed.Replace("\n", "<br>");
                result.Append(processed);
            }
        }

        var final = result.ToString();

        // Wrap in paragraph if we have content and it's not already wrapped
        if (!string.IsNullOrWhiteSpace(final) && !final.StartsWith("<"))
        {
            final = $"<p class=\"my-2\">{final}</p>";
        }

        return final;
    }
}
