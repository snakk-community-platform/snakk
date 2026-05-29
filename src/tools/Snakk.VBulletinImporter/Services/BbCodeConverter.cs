using System.Text.RegularExpressions;

namespace Snakk.VBulletinImporter.Services;

/// <summary>
/// Converts vBulletin BBCode to Snakk's markdown-like markup.
/// Snakk supports: **bold**, *italic*, [text](url), > blockquote,
/// `inline code`, ```code blocks```, - list items
/// </summary>
public static partial class BbCodeConverter
{
    public static string Convert(string bbcode)
    {
        if (string.IsNullOrEmpty(bbcode))
            return string.Empty;

        var text = bbcode;

        // Normalize line endings
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // === Code blocks first (protect from other transformations) ===
        // [code]...[/code] → ```\n...\n```
        text = CodeBlockRegex().Replace(text, m =>
            $"```\n{m.Groups[1].Value.Trim()}\n```");

        // [php]...[/php] → ```php\n...\n```
        text = PhpBlockRegex().Replace(text, m =>
            $"```php\n{m.Groups[1].Value.Trim()}\n```");

        // [html]...[/html] → ```html\n...\n```
        text = HtmlBlockRegex().Replace(text, m =>
            $"```html\n{m.Groups[1].Value.Trim()}\n```");

        // === Inline formatting ===
        // [b]...[/b] → **...**
        text = BoldRegex().Replace(text, "**$1**");

        // [i]...[/i] → *...*
        text = ItalicRegex().Replace(text, "*$1*");

        // [u]...[/u] → just keep text (no underline in Snakk markup)
        text = UnderlineRegex().Replace(text, "$1");

        // [s]...[/s] → ~~...~~ (strikethrough, common markdown)
        text = StrikeRegex().Replace(text, "~~$1~~");

        // === Links ===
        // [url=http://...]text[/url] → [text](url)
        text = UrlWithTextRegex().Replace(text, "[$2]($1)");

        // [url]http://...[/url] → [url](url)
        text = UrlPlainRegex().Replace(text, m =>
        {
            var url = m.Groups[1].Value;
            return $"[{url}]({url})";
        });

        // === Images ===
        // [img]http://...[/img] → [image](url)
        text = ImgRegex().Replace(text, "[$1]($1)");

        // === Quotes ===
        // [quote=username;postid]...[/quote] → > **username wrote:**\n> ...
        text = QuoteWithUserAndPostRegex().Replace(text, m =>
        {
            var username = m.Groups[1].Value;
            var content = m.Groups[2].Value.Trim();
            var quoted = PrefixLines(content, "> ");
            return $"> **{username} wrote:**\n{quoted}\n";
        });

        // [quote=username]...[/quote] → > **username wrote:**\n> ...
        text = QuoteWithUserRegex().Replace(text, m =>
        {
            var username = m.Groups[1].Value;
            var content = m.Groups[2].Value.Trim();
            var quoted = PrefixLines(content, "> ");
            return $"> **{username} wrote:**\n{quoted}\n";
        });

        // [quote]...[/quote] → > ...
        text = QuotePlainRegex().Replace(text, m =>
        {
            var content = m.Groups[1].Value.Trim();
            var quoted = PrefixLines(content, "> ");
            return $"{quoted}\n";
        });

        // === Lists ===
        // [list][*]item1[*]item2[/list] → - item1\n- item2
        text = ListRegex().Replace(text, m =>
        {
            var inner = m.Groups[1].Value;
            var items = ListItemRegex().Matches(inner);
            if (items.Count == 0) return inner;
            return string.Join("\n", items.Select(i => $"- {i.Groups[1].Value.Trim()}"));
        });

        // Ordered lists: [list=1][*]item[/list] → 1. item
        text = OrderedListRegex().Replace(text, m =>
        {
            var inner = m.Groups[1].Value;
            var items = ListItemRegex().Matches(inner);
            if (items.Count == 0) return inner;
            return string.Join("\n", items.Select((i, idx) => $"{idx + 1}. {i.Groups[1].Value.Trim()}"));
        });

        // === Media ===
        // [youtube]ID[/youtube] → [YouTube](https://www.youtube.com/watch?v=ID)
        text = YoutubeRegex().Replace(text, "[YouTube](https://www.youtube.com/watch?v=$1)");

        // [video]url[/video] → [Video](url)
        text = VideoRegex().Replace(text, "[Video]($1)");

        // === Unsupported tags: strip but keep content ===
        // [color=...], [size=...], [font=...], [align=...], [indent], [highlight]
        text = StripTagWithParamRegex().Replace(text, "$2");
        text = StripSimpleTagRegex().Replace(text, "$1");

        // [spoiler]...[/spoiler] → keep text with label
        text = SpoilerRegex().Replace(text, m =>
            $"**Spoiler:** {m.Groups[1].Value}");

        // === Cleanup ===
        // Strip any remaining unrecognized BBCode tags
        text = RemainingBbCodeRegex().Replace(text, "");

        // Collapse excessive blank lines (more than 2 → 2)
        text = ExcessiveNewlinesRegex().Replace(text, "\n\n");

        return text.Trim();
    }

    private static string PrefixLines(string text, string prefix)
    {
        var lines = text.Split('\n');
        return string.Join("\n", lines.Select(l => $"{prefix}{l}"));
    }

    // Code blocks
    [GeneratedRegex(@"\[code\](.*?)\[/code\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"\[php\](.*?)\[/php\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex PhpBlockRegex();

    [GeneratedRegex(@"\[html\](.*?)\[/html\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HtmlBlockRegex();

    // Inline formatting
    [GeneratedRegex(@"\[b\](.*?)\[/b\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"\[i\](.*?)\[/i\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"\[u\](.*?)\[/u\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex UnderlineRegex();

    [GeneratedRegex(@"\[s\](.*?)\[/s\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex StrikeRegex();

    // Links
    [GeneratedRegex(@"\[url=(.*?)\](.*?)\[/url\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex UrlWithTextRegex();

    [GeneratedRegex(@"\[url\](.*?)\[/url\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex UrlPlainRegex();

    // Images
    [GeneratedRegex(@"\[img\](.*?)\[/img\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ImgRegex();

    // Quotes
    [GeneratedRegex(@"\[quote=([^;\]]+);\d+\](.*?)\[/quote\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex QuoteWithUserAndPostRegex();

    [GeneratedRegex(@"\[quote=([^\]]+)\](.*?)\[/quote\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex QuoteWithUserRegex();

    [GeneratedRegex(@"\[quote\](.*?)\[/quote\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex QuotePlainRegex();

    // Lists
    [GeneratedRegex(@"\[list\](.*?)\[/list\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ListRegex();

    [GeneratedRegex(@"\[list=\d+\](.*?)\[/list\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex OrderedListRegex();

    [GeneratedRegex(@"\[\*\](.*?)(?=\[\*\]|\[/list\]|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ListItemRegex();

    // Media
    [GeneratedRegex(@"\[youtube\](.*?)\[/youtube\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex YoutubeRegex();

    [GeneratedRegex(@"\[video\](.*?)\[/video\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex VideoRegex();

    // Strip tags (keep content)
    [GeneratedRegex(@"\[(color|size|font|align|indent)=[^\]]*\](.*?)\[/\1\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex StripTagWithParamRegex();

    [GeneratedRegex(@"\[(indent|highlight|center|right|left)\](.*?)\[/\1\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex StripSimpleTagRegex();

    [GeneratedRegex(@"\[spoiler\](.*?)\[/spoiler\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex SpoilerRegex();

    // Cleanup
    [GeneratedRegex(@"\[/?[a-z][a-z0-9]*(?:=[^\]]+)?\]", RegexOptions.IgnoreCase)]
    private static partial Regex RemainingBbCodeRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveNewlinesRegex();
}
