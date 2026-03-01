using System.Text;
using Snakk.Protos.Search;

namespace Snakk.Web.Endpoints;

public static class SitemapEndpoints
{
    public static void MapSitemapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sitemap.xml", GenerateSitemapAsync)
            .ExcludeFromDescription();
    }

    private static async Task<IResult> GenerateSitemapAsync(
        SearchService.SearchServiceClient searchClient,
        HttpContext httpContext,
        IConfiguration configuration)
    {
        var baseUrl = configuration["WebBaseUrl"]
            ?? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

        var result = await searchClient.GetSitemapDiscussionsAsync(new SitemapRequest());

        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        // Homepage
        xml.AppendLine("  <url>");
        xml.AppendLine($"    <loc>{XmlEscape(baseUrl)}/</loc>");
        xml.AppendLine("    <changefreq>daily</changefreq>");
        xml.AppendLine("    <priority>1.0</priority>");
        xml.AppendLine("  </url>");

        // Hubs page
        xml.AppendLine("  <url>");
        xml.AppendLine($"    <loc>{XmlEscape(baseUrl)}/hubs</loc>");
        xml.AppendLine("    <changefreq>daily</changefreq>");
        xml.AppendLine("    <priority>0.9</priority>");
        xml.AppendLine("  </url>");

        // All discussions
        foreach (var discussion in result.Discussions)
        {
            var url = $"{baseUrl}/h/{discussion.HubSlug}/{discussion.SpaceSlug}/{discussion.Slug}~{discussion.PublicId}";
            var priority = discussion.IsPinned ? "0.9" : "0.7";
            var changefreq = discussion.IsPinned ? "weekly" : "monthly";

            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{XmlEscape(url)}</loc>");
            xml.AppendLine($"    <lastmod>{discussion.LastModified.ToDateTime():yyyy-MM-dd}</lastmod>");
            xml.AppendLine($"    <changefreq>{changefreq}</changefreq>");
            xml.AppendLine($"    <priority>{priority}</priority>");
            xml.AppendLine("  </url>");
        }

        xml.AppendLine("</urlset>");

        return Results.Content(xml.ToString(), "application/xml", Encoding.UTF8);
    }

    private static string XmlEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
