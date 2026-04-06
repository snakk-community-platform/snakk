namespace Snakk.Api.Endpoints;

using Snakk.Api.Extensions;
using Snakk.Application.Services;

public static class MediaEndpoints
{
    public static void MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/media")
            .WithTags("Media");

        group.MapPost("/upload", UploadMediaAsync)
            .WithName("UploadMedia")
            .RequireAuthorization()
            .RequireRateLimiting("api")
            .DisableAntiforgery(); // File uploads via fetch() — CSRF mitigated by JWT auth

        group.MapDelete("/draft", DeleteDraftMediaAsync)
            .WithName("DeleteDraftMedia")
            .RequireAuthorization();
    }

    private static async Task<IResult> DeleteDraftMediaAsync(
        string url,
        IMediaService mediaService,
        HttpContext context)
    {
        var userId = context.User.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var deleted = await mediaService.DeleteDraftAsync(url, userId.Value, context.RequestAborted);
        return deleted ? Results.Ok() : Results.NotFound();
    }

    private static async Task<IResult> UploadMediaAsync(
        IFormFile file,
        IMediaService mediaService,
        HttpContext context)
    {
        var userId = context.User.GetUserId();
        if (userId is null)
            return Results.Unauthorized();

        if (file is null || file.Length == 0)
            return Results.BadRequest(new { error = "No file provided." });

        try
        {
            using var stream = file.OpenReadStream();
            var result = await mediaService.UploadAsync(
                stream,
                file.FileName,
                file.ContentType,
                userId.Value,
                context.RequestAborted);

            return Results.Ok(new { result.PublicId, result.Url, result.ThumbnailUrl, result.MediumThumbnailUrl, result.BlurDataUri });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
