namespace Snakk.Application.Services;

using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Tables;
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
        .Build();

    private static readonly MarkdownPipeline PlainTextPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string ToHtml(string markup)
    {
        if (string.IsNullOrEmpty(markup))
            return string.Empty;

        var document = Markdown.Parse(markup, Pipeline);

        SanitizeLinks(document);
        CleanEmptyTables(document);
        NormalizeCodeBlockLanguages(document);

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();

        var html = writer.ToString().Trim();

        // Wrap tables in a scrollable container
        html = WrapTablesInScrollContainer(html);

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

    private static void NormalizeCodeBlockLanguages(MarkdownDocument document)
    {
        foreach (var fenced in document.Descendants<FencedCodeBlock>())
        {
            if (string.IsNullOrEmpty(fenced.Info))
                continue;

            var lang = fenced.Info.Trim().ToLowerInvariant();
            var attrs = fenced.GetAttributes();
            attrs.Classes?.Clear();
            attrs.AddClass($"language-{lang}");
        }
    }

    private static readonly Regex TableRegex = new(
        @"<table\b[^>]*>.*?</table>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static string WrapTablesInScrollContainer(string html) =>
        TableRegex.Replace(html, match => $"<div class=\"table-scroll\">{match.Value}</div>");

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
}
