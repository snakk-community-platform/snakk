using Microsoft.AspNetCore.Mvc;
using Snakk.Api.Authorization;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;

namespace Snakk.Api.Endpoints;

public static class HubManagementEndpoints
{
    public static void MapHubManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/c/{communitySlug}/h/{hubSlug}/manage")
            .WithTags("Hub Management")
            .RequireAuthorization();

        // Overview
        group.MapGet("/overview", async (
            [FromRoute] string communitySlug,
            [FromRoute] string hubSlug,
            [FromServices] IHubManagementService service,
            CancellationToken cancellationToken) =>
        {
            var overview = await service.GetOverviewAsync(communitySlug, hubSlug, cancellationToken);
            return overview != null ? Results.Ok(overview) : Results.NotFound();
        })
        .RequireHubModerator("hubSlug")
        .WithName("GetHubOverview");

        // Settings - Get
        group.MapGet("/settings", async (
            [FromRoute] string communitySlug,
            [FromRoute] string hubSlug,
            [FromServices] IHubManagementService service,
            CancellationToken cancellationToken) =>
        {
            var settings = await service.GetSettingsAsync(communitySlug, hubSlug, cancellationToken);
            return settings != null ? Results.Ok(settings) : Results.NotFound();
        })
        .RequireHubModerator("hubSlug")
        .WithName("GetHubSettings");

        // Settings - Update
        group.MapPut("/settings", async (
            [FromRoute] string communitySlug,
            [FromRoute] string hubSlug,
            [FromBody] UpdateHubSettingsRequest request,
            [FromServices] IHubManagementService service,
            CancellationToken cancellationToken) =>
        {
            var settings = await service.UpdateSettingsAsync(communitySlug, hubSlug, request, cancellationToken);
            return settings != null ? Results.Ok(settings) : Results.NotFound();
        })
        .RequireHubModerator("hubSlug")
        .WithName("UpdateHubSettings");

        // Moderation
        group.MapGet("/moderation", async (
            [FromRoute] string communitySlug,
            [FromRoute] string hubSlug,
            [FromServices] IHubManagementService service,
            CancellationToken cancellationToken) =>
        {
            var moderation = await service.GetModerationDataAsync(communitySlug, hubSlug, cancellationToken);
            return Results.Ok(moderation);
        })
        .RequireHubModerator("hubSlug")
        .WithName("GetHubModeration");

        // Spaces
        group.MapGet("/spaces", async (
            [FromRoute] string communitySlug,
            [FromRoute] string hubSlug,
            [FromServices] IHubManagementService service,
            CancellationToken cancellationToken) =>
        {
            var spaces = await service.GetSpacesAsync(communitySlug, hubSlug, cancellationToken);
            return Results.Ok(spaces);
        })
        .RequireHubModerator("hubSlug")
        .WithName("GetHubSpaces");
    }
}
