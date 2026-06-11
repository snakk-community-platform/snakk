using System.Text.Json;
using System.Text.RegularExpressions;
using Markdig;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Pages;

public class HelpModel : PageModel
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex HeadingRegex = new(
        @"<h2[^>]*\bid=""([^""]+)""[^>]*>(.*?)</h2>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    public string Slug { get; private set; } = "what-is-snakk";
    public string ArticleTitle { get; private set; } = "";
    public string SectionTitle { get; private set; } = "";
    public string RenderedHtml { get; private set; } = "";
    public List<HelpSection> Sections { get; private set; } = [];
    public HelpArticle? PrevArticle { get; private set; }
    public HelpArticle? NextArticle { get; private set; }
    public List<(string Id, string Text)> TableOfContents { get; private set; } = [];

    public IActionResult OnGet(string? slug)
    {
        var contentRoot = Path.Combine(
            Directory.GetCurrentDirectory(), "Content", "Help");

        var manifestPath = Path.Combine(contentRoot, "manifest.json");
        if (!System.IO.File.Exists(manifestPath))
            return NotFound();

        var manifestJson = System.IO.File.ReadAllText(manifestPath);
        Sections = JsonSerializer.Deserialize<List<HelpSection>>(manifestJson, JsonOptions) ?? [];

        Slug = string.IsNullOrWhiteSpace(slug) ? "what-is-snakk" : slug.Trim().ToLowerInvariant();

        var article = Sections
            .SelectMany(s => s.Items)
            .FirstOrDefault(a => a.Slug == Slug);

        if (article is null)
            return RedirectToPage("/Help", new { slug = "what-is-snakk" });

        ArticleTitle = article.Title;

        var section = Sections.FirstOrDefault(s => s.Items.Any(a => a.Slug == Slug));
        SectionTitle = section?.Title ?? "";

        var flat = Sections.SelectMany(s => s.Items).ToList();
        var idx = flat.FindIndex(a => a.Slug == Slug);
        PrevArticle = idx > 0 ? flat[idx - 1] : null;
        NextArticle = idx < flat.Count - 1 ? flat[idx + 1] : null;

        var mdPath = Path.Combine(contentRoot, $"{Slug}.md");
        if (System.IO.File.Exists(mdPath))
        {
            var markdown = System.IO.File.ReadAllText(mdPath);
            RenderedHtml = Markdown.ToHtml(markdown, Pipeline);
        }
        else
        {
            RenderedHtml = "<p>This article is coming soon.</p>";
        }

        TableOfContents = HeadingRegex.Matches(RenderedHtml)
            .Select(m => (m.Groups[1].Value, TagRegex.Replace(m.Groups[2].Value, "")))
            .ToList();

        return Page();
    }
}

public class HelpSection
{
    public string Title { get; set; } = "";
    public List<HelpArticle> Items { get; set; } = [];
}

public class HelpArticle
{
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
}
