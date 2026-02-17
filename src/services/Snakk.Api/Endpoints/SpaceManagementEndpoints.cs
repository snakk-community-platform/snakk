using Microsoft.AspNetCore.Mvc;
using Snakk.Api.Authorization;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;

namespace Snakk.Api.Endpoints;

public static class SpaceManagementEndpoints
{
    public static void MapSpaceManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/c/{communitySlug}/s/{spaceSlug}/manage")
            .WithTags("Space Management")
            .RequireAuthorization();

        // Overview
        group.MapGet("/overview", async (
            [FromRoute] string communitySlug,
            [FromRoute] string spaceSlug,
            [FromServices] ISpaceManagementService service,
            CancellationToken cancellationToken) =>
        {
            var overview = await service.GetOverviewAsync(communitySlug, spaceSlug, cancellationToken);
            return overview != null ? Results.Ok(overview) : Results.NotFound();
        })
        .RequireSpaceModerator("spaceSlug")
        .WithName("GetSpaceOverview");

        // Settings - Get
        group.MapGet("/settings", async (
            [FromRoute] string communitySlug,
            [FromRoute] string spaceSlug,
            [FromServices] ISpaceManagementService service,
            CancellationToken cancellationToken) =>
        {
            var settings = await service.GetSettingsAsync(communitySlug, spaceSlug, cancellationToken);
            return settings != null ? Results.Ok(settings) : Results.NotFound();
        })
        .RequireSpaceModerator("spaceSlug")
        .WithName("GetSpaceSettings");

        // Settings - Update
        group.MapPut("/settings", async (
            [FromRoute] string communitySlug,
            [FromRoute] string spaceSlug,
            [FromBody] UpdateSpaceSettingsRequest request,
            [FromServices] ISpaceManagementService service,
            CancellationToken cancellationToken) =>
        {
            var settings = await service.UpdateSettingsAsync(communitySlug, spaceSlug, request, cancellationToken);
            return settings != null ? Results.Ok(settings) : Results.NotFound();
        })
        .RequireSpaceModerator("spaceSlug")
        .WithName("UpdateSpaceSettings");

        // Moderation
        group.MapGet("/moderation", async (
            [FromRoute] string communitySlug,
            [FromRoute] string spaceSlug,
            [FromServices] ISpaceManagementService service,
            CancellationToken cancellationToken) =>
        {
            var moderation = await service.GetModerationDataAsync(communitySlug, spaceSlug, cancellationToken);
            return Results.Ok(moderation);
        })
        .RequireSpaceModerator("spaceSlug")
        .WithName("GetSpaceModeration");

        // Rules - Get
        group.MapGet("/rules", async (
            [FromRoute] string communitySlug,
            [FromRoute] string spaceSlug,
            [FromServices] ISpaceManagementService service,
            CancellationToken cancellationToken) =>
        {
            var rules = await service.GetRulesAsync(communitySlug, spaceSlug, cancellationToken);
            return Results.Ok(rules);
        })
        .RequireSpaceModerator("spaceSlug")
        .WithName("GetSpaceRules");

        // Rules - Update
        group.MapPut("/rules", async (
            [FromRoute] string communitySlug,
            [FromRoute] string spaceSlug,
            [FromBody] UpdateSpaceRulesRequest request,
            [FromServices] ISpaceManagementService service,
            CancellationToken cancellationToken) =>
        {
            var rules = await service.UpdateRulesAsync(communitySlug, spaceSlug, request, cancellationToken);
            return Results.Ok(rules);
        })
        .RequireSpaceModerator("spaceSlug")
        .WithName("UpdateSpaceRules");
    }
}
