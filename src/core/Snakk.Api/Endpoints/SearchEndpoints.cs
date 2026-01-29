namespace Snakk.Api.Endpoints;

using Snakk.Application.UseCases;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/search")
            .WithTags("Search");

        group.MapGet("/discussions", SearchDiscussionsAsync)
            .WithName("SearchDiscussions");

        group.MapGet("/posts", SearchPostsAsync)
            .WithName("SearchPosts");
    }

    private static async Task<IResult> SearchDiscussionsAsync(
        string? q,
        string? authorPublicId,
        string? spacePublicId,
        string? hubPublicId,
        int offset,
        int pageSize,
        SearchUseCase searchUseCase)
    {
        // Validate pageSize
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var results = await searchUseCase.SearchDiscussionsAsync(
            q ?? "",
            authorPublicId,
            spacePublicId,
            hubPublicId,
            offset,
            pageSize);

        return Results.Ok(results);
    }

    private static async Task<IResult> SearchPostsAsync(
        string? q,
        string? authorPublicId,
        string? discussionPublicId,
        string? spacePublicId,
        int offset,
        int pageSize,
        SearchUseCase searchUseCase)
    {
        // Validate pageSize
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var results = await searchUseCase.SearchPostsAsync(
            q ?? "",
            authorPublicId,
            discussionPublicId,
            spacePublicId,
            offset,
            pageSize);

        return Results.Ok(results);
    }
}
