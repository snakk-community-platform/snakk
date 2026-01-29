namespace Snakk.Api.Endpoints;

using Snakk.Infrastructure.Rendering;

public static class MarkupEndpoints
{
    public static void MapMarkupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/markup")
            .WithTags("Markup");

        group.MapPost("/preview", PreviewMarkupAsync)
            .WithName("PreviewMarkupHtmx");
    }

    private static async Task<IResult> PreviewMarkupAsync(
        HttpRequest request,
        IMarkupParser markupParser)
    {
        using var reader = new StreamReader(request.Body);
        var content = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(content))
            return Results.Content("<p class=\"text-base-content/50 italic\">Nothing to preview</p>", "text/html");

        var html = markupParser.ToHtml(content);

        return Results.Content($"<div class=\"prose prose-sm max-w-none\">{html}</div>", "text/html");
    }
}
