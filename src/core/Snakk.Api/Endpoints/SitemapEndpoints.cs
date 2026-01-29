namespace Snakk.Api.Endpoints;

using System.Text;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;

public static class SitemapEndpoints
{
    public static void MapSitemapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sitemap.xml", GenerateSitemapAsync)
            .WithName("GetSitemap")
            .Produces<string>(200, "application/xml");
    }

    private static async Task<IResult> GenerateSitemapAsync(
        SnakkDbContext dbContext,
        HttpContext httpContext,
        IConfiguration configuration)
    {
        var baseUrl = configuration["WebBaseUrl"] ?? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

        // Get all discussions with their space and hub info
        var discussions = await dbContext.Discussions
            .AsNoTracking()
            .Include(d => d.Space)
                .ThenInclude(s => s.Hub)
                    .ThenInclude(h => h.Community)
            .Where(d => !d.IsDeleted)
            .OrderByDescending(d => d.LastModifiedAt ?? d.CreatedAt)
            .Select(d => new
            {
                d.PublicId,
                d.Title,
                d.Slug,
                HubSlug = d.Space.Hub.Slug,
                SpaceSlug = d.Space.Slug,
                CommunitySlug = d.Space.Hub.Community.Slug,
                LastModified = d.LastModifiedAt ?? d.CreatedAt,
                d.IsPinned
            })
            .ToListAsync();

        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        // Add homepage
        xml.AppendLine("  <url>");
        xml.AppendLine($"    <loc>{XmlEscape(baseUrl)}/</loc>");
        xml.AppendLine("    <changefreq>daily</changefreq>");
        xml.AppendLine("    <priority>1.0</priority>");
        xml.AppendLine("  </url>");

        // Add hubs page
        xml.AppendLine("  <url>");
        xml.AppendLine($"    <loc>{XmlEscape(baseUrl)}/hubs</loc>");
        xml.AppendLine("    <changefreq>daily</changefreq>");
        xml.AppendLine("    <priority>0.9</priority>");
        xml.AppendLine("  </url>");

        // Add all discussions
        foreach (var discussion in discussions)
        {
            var url = $"{baseUrl}/h/{discussion.HubSlug}/{discussion.SpaceSlug}/{discussion.Slug}~{discussion.PublicId}";
            var priority = discussion.IsPinned ? "0.9" : "0.7";
            var changefreq = discussion.IsPinned ? "weekly" : "monthly";

            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{XmlEscape(url)}</loc>");
            xml.AppendLine($"    <lastmod>{discussion.LastModified:yyyy-MM-dd}</lastmod>");
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
