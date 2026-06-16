using System.Text;

namespace Snakk.Web.Endpoints;

public static class RobotsEndpoints
{
    public static void MapRobotsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/robots.txt", (HttpRequest req) =>
        {
            var baseUrl = $"{req.Scheme}://{req.Host}";
            var content = $"""
                # Snakk Forum - Robots.txt

                User-agent: *
                Allow: /
                Disallow: /api/
                Disallow: /Auth/
                Disallow: /u/*/settings
                Disallow: /notifications

                # Sitemap
                Sitemap: {baseUrl}/sitemap.xml

                """;
            return Results.Content(content, "text/plain", Encoding.UTF8);
        }).ExcludeFromDescription();
    }
}
