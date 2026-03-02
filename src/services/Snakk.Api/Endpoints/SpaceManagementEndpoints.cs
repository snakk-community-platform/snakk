using Microsoft.AspNetCore.Mvc;
using Snakk.Api.Authorization;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;

namespace Snakk.Api.Endpoints;

public static class SpaceManagementEndpoints
{
    public static void MapSpaceManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/spaces/{spaceId}/manage")
            .WithTags("Space Management")
            .RequireAuthorization();

        // Overview
        group.MapGet("/overview", async (
            [FromRoute] string spaceId,
            [FromServices] ISpaceManagementService service,
            CancellationToken cancellationToken) =>
        {
            var overview = await service.GetOverviewAsync(spaceId, cancellationToken);
            return overview is not null ? Results.Ok(overview) : Results.NotFound();
        })
        .RequireSpaceModerator("spaceId")
        .WithName("GetSpaceOverview");

        // Settings - Get
        group.MapGet("/settings", async (
            [FromRoute] string spaceId,
            [FromServices] ISpaceManagementService service,
            CancellationToken cancellationToken) =>
        {
            var settings = await service.GetSettingsAsync(spaceId, cancellationToken);
            return settings is not null ? Results.Ok(settings) : Results.NotFound();
        })
        .RequireSpaceModerator("spaceId")
        .WithName("GetSpaceSettings");

        // Settings - Update
        group.MapPut("/settings", async (
            [FromRoute] string spaceId,
            [FromBody] UpdateSpaceSettingsRequest request,
            [FromServices] ISpaceManagementService service,
            CancellationToken cancellationToken) =>
        {
            var settings = await service.UpdateSettingsAsync(spaceId, request, cancellationToken);
            return settings is not null ? Results.Ok(settings) : Results.NotFound();
        })
        .RequireSpaceModerator("spaceId")
        .WithName("UpdateSpaceSettings");

        // Moderation
        group.MapGet("/moderation", async (
            [FromRoute] string spaceId,
            [FromServices] ISpaceManagementService service,
            CancellationToken cancellationToken) =>
        {
            var moderation = await service.GetModerationDataAsync(spaceId, cancellationToken);
            return Results.Ok(moderation);
        })
        .RequireSpaceModerator("spaceId")
        .WithName("GetSpaceModeration");

        // Rules - Get
        group.MapGet("/rules", async (
            [FromRoute] string spaceId,
            [FromServices] ISpaceManagementService service,
            CancellationToken cancellationToken) =>
        {
            var rules = await service.GetRulesAsync(spaceId, cancellationToken);
            return Results.Ok(rules);
        })
        .RequireSpaceModerator("spaceId")
        .WithName("GetSpaceRulesManagement");

        // Rules - Update
        group.MapPut("/rules", async (
            [FromRoute] string spaceId,
            [FromBody] UpdateSpaceRulesRequest request,
            [FromServices] ISpaceManagementService service,
            CancellationToken cancellationToken) =>
        {
            var rules = await service.UpdateRulesAsync(spaceId, request, cancellationToken);
            return Results.Ok(rules);
        })
        .RequireSpaceModerator("spaceId")
        .WithName("UpdateSpaceRules");
    }
}
