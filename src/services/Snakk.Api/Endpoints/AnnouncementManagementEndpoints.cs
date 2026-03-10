namespace Snakk.Api.Endpoints;

using System.Security.Claims;
using Snakk.Api.Models;
using Snakk.Application.DTOs.Responses;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public static class AnnouncementManagementEndpoints
{
    public static void MapAnnouncementManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/announcements")
            .WithTags("Announcements")
            .RequireAuthorization();

        group.MapPost("/", CreateAnnouncementAsync)
            .WithName("CreateAnnouncement")
            .Produces<AnnouncementResponse>(StatusCodes.Status201Created);

        group.MapGet("/", GetAnnouncementsByScopeAsync)
            .WithName("GetAnnouncementsByScope")
            .Produces<IEnumerable<AnnouncementResponse>>();

        group.MapGet("/{publicId}", GetAnnouncementAsync)
            .WithName("GetAnnouncement")
            .Produces<AnnouncementResponse>();

        group.MapPut("/{publicId}", UpdateAnnouncementAsync)
            .WithName("UpdateAnnouncement")
            .Produces<AnnouncementResponse>();

        group.MapDelete("/{publicId}", DeleteAnnouncementAsync)
            .WithName("DeleteAnnouncement")
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> CreateAnnouncementAsync(
        CreateAnnouncementRequest request,
        ClaimsPrincipal user,
        AnnouncementUseCase announcementUseCase)
    {
        var userId = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (!Enum.TryParse<AnnouncementScopeEnum>(request.Scope, true, out var scope))
            return Results.BadRequest(new { error = $"Invalid scope: {request.Scope}" });

        if (!Enum.TryParse<AnnouncementTypeEnum>(request.Type, true, out var type))
            return Results.BadRequest(new { error = $"Invalid type: {request.Type}" });

        var result = await announcementUseCase.CreateAsync(
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

    private static async Task<IResult> GetAnnouncementsByScopeAsync(
        string scope,
        string scopeEntityId,
        AnnouncementUseCase announcementUseCase)
    {
        if (!Enum.TryParse<AnnouncementScopeEnum>(scope, true, out var scopeEnum))
            return Results.BadRequest(new { error = $"Invalid scope: {scope}" });

        var announcements = await announcementUseCase.GetByScopeAsync(scopeEnum, scopeEntityId);

        return Results.Ok(announcements.Select(ToResponse));
    }

    private static async Task<IResult> GetAnnouncementAsync(
        string publicId,
        AnnouncementUseCase announcementUseCase)
    {
        var result = await announcementUseCase.GetByIdAsync(AnnouncementId.From(publicId));

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return Results.Ok(ToResponse(result.Value!));
    }

    private static async Task<IResult> UpdateAnnouncementAsync(
        string publicId,
        UpdateAnnouncementRequest request,
        AnnouncementUseCase announcementUseCase)
    {
        if (!Enum.TryParse<AnnouncementTypeEnum>(request.Type, true, out var type))
            return Results.BadRequest(new { error = $"Invalid type: {request.Type}" });

        var result = await announcementUseCase.UpdateAsync(
            AnnouncementId.From(publicId),
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

    private static async Task<IResult> DeleteAnnouncementAsync(
        string publicId,
        AnnouncementUseCase announcementUseCase)
    {
        var result = await announcementUseCase.DeleteAsync(AnnouncementId.From(publicId));

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return Results.NoContent();
    }

    private static AnnouncementResponse ToResponse(Domain.Entities.Announcement a) =>
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
