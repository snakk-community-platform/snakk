namespace Snakk.Application.Services;

using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

public static class ContentBlockAnalyzer
{
    public const int MaxBlocksPerType = 3;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static ContentBlockCounts Count(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return new ContentBlockCounts();

        var doc = Markdown.Parse(markdown, Pipeline);

        int charts = 0, codeBlocks = 0, callouts = 0, imageBlocks = 0;
        foreach (var fenced in doc.Descendants<FencedCodeBlock>())
        {
            var info = fenced.Info?.Trim() ?? string.Empty;
            if (string.Equals(info, "chart", StringComparison.OrdinalIgnoreCase))
                charts++;
            else if (info.StartsWith("image-group", StringComparison.OrdinalIgnoreCase))
                imageBlocks++;
            else if (info.StartsWith("callout-", StringComparison.OrdinalIgnoreCase))
                callouts++;
            else
                codeBlocks++;
        }

        return new ContentBlockCounts(
            Charts:           charts,
            CodeBlocks:       codeBlocks,
            Lists:            doc.Descendants<ListBlock>().Count(),
            Callouts:         callouts,
            HorizontalRules:  doc.Descendants<ThematicBreakBlock>().Count(),
            Tables:           doc.Descendants<Table>().Count(),
            Blockquotes:      doc.Descendants<QuoteBlock>().Count(),
            ImageBlocks:      imageBlocks);
    }
}

public record ContentBlockCounts(
    int Charts          = 0,
    int CodeBlocks      = 0,
    int Lists           = 0,
    int Callouts        = 0,
    int HorizontalRules = 0,
    int Tables          = 0,
    int Blockquotes     = 0,
    int ImageBlocks     = 0);
