namespace Snakk.Api.Endpoints;

using System.Security.Claims;
using Snakk.Api.Models;
using Snakk.Application.DTOs.Responses;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public static class BannerManagementEndpoints
{
    public static void MapBannerManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/announcements")
            .WithTags("Banners")
            .RequireAuthorization();

        group.MapPost("/", CreateBannerAsync)
            .WithName("CreateBanner")
            .Produces<BannerResponse>(StatusCodes.Status201Created);

        group.MapGet("/", GetBannersByScopeAsync)
            .WithName("GetBannersByScope")
            .Produces<IEnumerable<BannerResponse>>();

        group.MapGet("/{publicId}", GetBannerAsync)
            .WithName("GetBanner")
            .Produces<BannerResponse>();

        group.MapPut("/{publicId}", UpdateBannerAsync)
            .WithName("UpdateBanner")
            .Produces<BannerResponse>();

        group.MapDelete("/{publicId}", DeleteBannerAsync)
            .WithName("DeleteBanner")
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> CreateBannerAsync(
        CreateBannerRequest request,
        ClaimsPrincipal user,
        BannerUseCase bannerUseCase)
    {
        var userId = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (!Enum.TryParse<BannerScopeEnum>(request.Scope, true, out var scope))
            return Results.BadRequest(new { error = $"Invalid scope: {request.Scope}" });

        if (!Enum.TryParse<BannerTypeEnum>(request.Type, true, out var type))
            return Results.BadRequest(new { error = $"Invalid type: {request.Type}" });

        var result = await bannerUseCase.CreateAsync(
            scope,
            request.ScopeEntityId,
            UserId.From(userId),
            request.Title,
            request.Content,
            type,
            request.VisibleFrom,
            request.VisibleUntil,
            request.IsDismissible,
            request.SortOrder);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        var a = result.Value!;

        return Results.Created($"/announcements/{a.PublicId.Value}", ToResponse(a));
    }

    private static async Task<IResult> GetBannersByScopeAsync(
        string scope,
        string scopeEntityId,
        BannerUseCase bannerUseCase)
    {
        if (!Enum.TryParse<BannerScopeEnum>(scope, true, out var scopeEnum))
            return Results.BadRequest(new { error = $"Invalid scope: {scope}" });

        var announcements = await bannerUseCase.GetByScopeAsync(scopeEnum, scopeEntityId);

        return Results.Ok(announcements.Select(ToResponse));
    }

    private static async Task<IResult> GetBannerAsync(
        string publicId,
        BannerUseCase bannerUseCase)
    {
        var result = await bannerUseCase.GetByIdAsync(BannerId.From(publicId));

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return Results.Ok(ToResponse(result.Value!));
    }

    private static async Task<IResult> UpdateBannerAsync(
        string publicId,
        UpdateBannerRequest request,
        BannerUseCase bannerUseCase)
    {
        if (!Enum.TryParse<BannerTypeEnum>(request.Type, true, out var type))
            return Results.BadRequest(new { error = $"Invalid type: {request.Type}" });

        var result = await bannerUseCase.UpdateAsync(
            BannerId.From(publicId),
            request.Title,
            request.Content,
            type,
            request.VisibleFrom,
            request.VisibleUntil,
            request.IsDismissible,
            request.SortOrder);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(ToResponse(result.Value!));
    }

    private static async Task<IResult> DeleteBannerAsync(
        string publicId,
        BannerUseCase bannerUseCase)
    {
        var result = await bannerUseCase.DeleteAsync(BannerId.From(publicId));

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return Results.NoContent();
    }

    private static BannerResponse ToResponse(Domain.Entities.Banner a) =>
        new(
            a.PublicId.Value,
            a.Title,
            a.Content,
            a.RenderedContent,
            a.Type.ToString(),
            a.Scope.ToString(),
            a.ScopeEntityId,
            a.VisibleFrom,
            a.VisibleUntil,
            a.IsDismissible,
            a.SortOrder,
            a.CreatedAt,
            a.LastModifiedAt,
            a.CreatedByUserId.Value);
}
