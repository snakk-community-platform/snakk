using System.Collections.Concurrent;
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

    // Manifest parsed once per process — content is deployment-static.
    private static readonly Lazy<HelpManifestCache> _manifest = new(() =>
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Help", "manifest.json");
        if (!System.IO.File.Exists(path)) return new HelpManifestCache();
        var json = System.IO.File.ReadAllText(path);
        var sections = JsonSerializer.Deserialize<List<HelpSection>>(json, JsonOptions) ?? [];
        return HelpManifestCache.Build(sections);
    });

    // Rendered HTML + TOC cached per slug — populated on first access, permanent until restart.
    private static readonly ConcurrentDictionary<string, RenderedArticle> _renderCache = new();

    public string Slug { get; private set; } = "what-is-snakk";
    public string ArticleTitle { get; private set; } = "";
    public string SectionTitle { get; private set; } = "";
    public string RenderedHtml { get; private set; } = "";
    public List<HelpSection> Sections { get; private set; } = [];
    public HelpArticle? PrevArticle { get; private set; }
    public HelpArticle? NextArticle { get; private set; }
    public List<(string Id, string Text)> TableOfContents { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string? slug)
    {
        var manifest = _manifest.Value;
        if (manifest.Sections.Count == 0) return NotFound();

        Sections = manifest.Sections;
        Slug = string.IsNullOrWhiteSpace(slug) ? "what-is-snakk" : slug.Trim().ToLowerInvariant();

        if (!manifest.Lookup.TryGetValue(Slug, out var ctx))
            return RedirectToPage("/Help", new { slug = "what-is-snakk" });

        ArticleTitle = ctx.Article.Title;
        SectionTitle = ctx.SectionTitle;
        PrevArticle  = ctx.Prev;
        NextArticle  = ctx.Next;

        var contentRoot = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Help");
        var rendered = await GetOrRenderAsync(Slug, contentRoot);
        RenderedHtml    = rendered.Html;
        TableOfContents = rendered.Toc;

        return Page();
    }

    private static async Task<RenderedArticle> GetOrRenderAsync(string slug, string contentRoot)
    {
        if (_renderCache.TryGetValue(slug, out var cached))
            return cached;

        var mdPath = Path.Combine(contentRoot, $"{slug}.md");
        var html = System.IO.File.Exists(mdPath)
            ? Markdown.ToHtml(await System.IO.File.ReadAllTextAsync(mdPath), Pipeline)
            : "<p>This article is coming soon.</p>";

        var toc = HeadingRegex.Matches(html)
            .Select(m => (m.Groups[1].Value, TagRegex.Replace(m.Groups[2].Value, "")))
            .ToList();

        var result = new RenderedArticle(html, toc);
        _renderCache.TryAdd(slug, result);
        return result;
    }

    private record RenderedArticle(string Html, List<(string Id, string Text)> Toc);

    private record ArticleContext(
        HelpArticle Article, string SectionTitle, HelpArticle? Prev, HelpArticle? Next);

    private class HelpManifestCache
    {
        public List<HelpSection> Sections { get; init; } = [];
        public Dictionary<string, ArticleContext> Lookup { get; init; } = [];

        public static HelpManifestCache Build(List<HelpSection> sections)
        {
            var flat = sections.SelectMany(s => s.Items).ToList();
            var lookup = new Dictionary<string, ArticleContext>(flat.Count);
            for (var i = 0; i < flat.Count; i++)
            {
                var a = flat[i];
                var sec = sections.First(s => s.Items.Contains(a));
                lookup[a.Slug] = new ArticleContext(a, sec.Title,
                    i > 0 ? flat[i - 1] : null,
                    i < flat.Count - 1 ? flat[i + 1] : null);
            }
            return new HelpManifestCache { Sections = sections, Lookup = lookup };
        }
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
