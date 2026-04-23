namespace Snakk.Api.Endpoints;

using Snakk.Api.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Shared;

public static class SocialLinkEndpoints
{
    public static void MapSocialLinkEndpoints(this IEndpointRouteBuilder app)
    {
        var meGroup = app.MapGroup("/me/social")
            .WithTags("SocialLinks")
            .RequireAuthorization();

        meGroup.MapGet("/", GetMySocialLinksAsync).WithName("GetMySocialLinks");
        meGroup.MapPut("/", UpdateMySocialLinksAsync).WithName("UpdateMySocialLinks");

        app.MapGet("/users/{publicId}/social", GetUserSocialLinksAsync)
            .WithName("GetUserSocialLinks")
            .WithTags("SocialLinks");
    }

    private static async Task<IResult> GetMySocialLinksAsync(
        ICurrentUserService currentUser,
        AuthenticationUseCase authUseCase)
    {
        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null) return Results.Unauthorized();

        var links = await authUseCase.GetMySocialLinksAsync(UserId.From(userIdValue));
        return Results.Ok(new { links = BuildResponse(links) });
    }

    private static async Task<IResult> UpdateMySocialLinksAsync(
        UpdateSocialLinksRequest request,
        ICurrentUserService currentUser,
        AuthenticationUseCase authUseCase)
    {
        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null) return Results.Unauthorized();

        var rawLinks = (request.Links ?? [])
            .Select(l => (l.Platform ?? "", l.Value ?? ""))
            .ToList();

        var result = await authUseCase.UpdateSocialLinksAsync(UserId.From(userIdValue), rawLinks);

        return result.IsSuccess
            ? Results.Ok(new { message = "Social links updated." })
            : Results.BadRequest(new { error = result.Error });
    }

    private static async Task<IResult> GetUserSocialLinksAsync(
        string publicId,
        AuthenticationUseCase authUseCase)
    {
        var links = await authUseCase.GetSocialLinksByPublicIdAsync(publicId);
        return Results.Ok(new { links = BuildResponse(links) });
    }

    private static List<SocialLinkResponse> BuildResponse(List<(string Platform, string Username)> links)
        => links.ConvertAll(l =>
        {
            SocialPlatformRegistry.All.TryGetValue(l.Platform, out var platform);
            return new SocialLinkResponse(
                l.Platform,
                platform?.DisplayName ?? l.Platform,
                l.Username,
                platform is not null ? SocialPlatformRegistry.BuildUrl(platform, l.Username) : null);
        });
}

public record SocialLinkResponse(string Platform, string DisplayName, string Username, string? Url);
public record SocialLinkInput(string? Platform, string? Value);
public record UpdateSocialLinksRequest(List<SocialLinkInput>? Links);
