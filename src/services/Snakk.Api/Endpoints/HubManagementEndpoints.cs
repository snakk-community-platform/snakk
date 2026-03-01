using Microsoft.AspNetCore.Mvc;
using Snakk.Api.Authorization;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;

namespace Snakk.Api.Endpoints;

public static class HubManagementEndpoints
{
    public static void MapHubManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/hubs/{hubId}/manage")
            .WithTags("Hub Management")
            .RequireAuthorization();

        // Overview
        group.MapGet("/overview", async (
            [FromRoute] string hubId,
            [FromServices] IHubManagementService service,
            CancellationToken cancellationToken) =>
        {
            var overview = await service.GetOverviewAsync(hubId, cancellationToken);
            return overview != null ? Results.Ok(overview) : Results.NotFound();
        })
        .RequireHubModerator("hubId")
        .WithName("GetHubOverview");

        // Settings - Get
        group.MapGet("/settings", async (
            [FromRoute] string hubId,
            [FromServices] IHubManagementService service,
            CancellationToken cancellationToken) =>
        {
            var settings = await service.GetSettingsAsync(hubId, cancellationToken);
            return settings != null ? Results.Ok(settings) : Results.NotFound();
        })
        .RequireHubModerator("hubId")
        .WithName("GetHubSettings");

        // Settings - Update
        group.MapPut("/settings", async (
            [FromRoute] string hubId,
            [FromBody] UpdateHubSettingsRequest request,
            [FromServices] IHubManagementService service,
            CancellationToken cancellationToken) =>
        {
            var settings = await service.UpdateSettingsAsync(hubId, request, cancellationToken);
            return settings != null ? Results.Ok(settings) : Results.NotFound();
        })
        .RequireHubModerator("hubId")
        .WithName("UpdateHubSettings");

        // Moderation
        group.MapGet("/moderation", async (
            [FromRoute] string hubId,
            [FromServices] IHubManagementService service,
            CancellationToken cancellationToken) =>
        {
            var moderation = await service.GetModerationDataAsync(hubId, cancellationToken);
            return Results.Ok(moderation);
        })
        .RequireHubModerator("hubId")
        .WithName("GetHubModeration");

        // Spaces
        group.MapGet("/spaces", async (
            [FromRoute] string hubId,
            [FromServices] IHubManagementService service,
            CancellationToken cancellationToken) =>
        {
            var spaces = await service.GetSpacesAsync(hubId, cancellationToken);
            return Results.Ok(spaces);
        })
        .RequireHubModerator("hubId")
        .WithName("GetHubSpaces");

        // Rules - Get
        group.MapGet("/rules", async (
            [FromRoute] string hubId,
            [FromServices] IHubManagementService service,
            CancellationToken cancellationToken) =>
        {
            var rules = await service.GetRulesAsync(hubId, cancellationToken);
            return Results.Ok(rules);
        })
        .RequireHubModerator("hubId")
        .WithName("GetHubRulesManagement");

        // Rules - Update
        group.MapPut("/rules", async (
            [FromRoute] string hubId,
            [FromBody] UpdateHubRulesRequest request,
            [FromServices] IHubManagementService service,
            CancellationToken cancellationToken) =>
        {
            var rules = await service.UpdateRulesAsync(hubId, request, cancellationToken);
            return Results.Ok(rules);
        })
        .RequireHubModerator("hubId")
        .WithName("UpdateHubRules");
    }
}
