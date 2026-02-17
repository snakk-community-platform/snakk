using Microsoft.AspNetCore.Mvc;
using Snakk.Api.Authorization;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;

namespace Snakk.Api.Endpoints;

public static class CommunityManagementEndpoints
{
    public static void MapCommunityManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/c/{slug}/manage")
            .WithTags("Community Management")
            .RequireAuthorization();

        // Overview
        group.MapGet("/overview", async (
            [FromRoute] string slug,
            [FromServices] ICommunityManagementService service,
            CancellationToken cancellationToken) =>
        {
            var overview = await service.GetOverviewAsync(slug, cancellationToken);
            return overview != null ? Results.Ok(overview) : Results.NotFound();
        })
        .RequireCommunityAdmin("slug")
        .WithName("GetCommunityOverview");

        // Settings - Get
        group.MapGet("/settings", async (
            [FromRoute] string slug,
            [FromServices] ICommunityManagementService service,
            CancellationToken cancellationToken) =>
        {
            var settings = await service.GetSettingsAsync(slug, cancellationToken);
            return settings != null ? Results.Ok(settings) : Results.NotFound();
        })
        .RequireCommunityAdmin("slug")
        .WithName("GetCommunitySettings");

        // Settings - Update
        group.MapPut("/settings", async (
            [FromRoute] string slug,
            [FromBody] UpdateCommunitySettingsRequest request,
            [FromServices] ICommunityManagementService service,
            CancellationToken cancellationToken) =>
        {
            var settings = await service.UpdateSettingsAsync(slug, request, cancellationToken);
            return settings != null ? Results.Ok(settings) : Results.NotFound();
        })
        .RequireCommunityAdmin("slug")
        .WithName("UpdateCommunitySettings");

        // Moderation
        group.MapGet("/moderation", async (
            [FromRoute] string slug,
            [FromServices] ICommunityManagementService service,
            CancellationToken cancellationToken) =>
        {
            var moderation = await service.GetModerationDataAsync(slug, cancellationToken);
            return Results.Ok(moderation);
        })
        .RequireCommunityAdmin("slug")
        .WithName("GetCommunityModeration");

        // Members - List
        group.MapGet("/members", async (
            [FromRoute] string slug,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromServices] ICommunityManagementService service,
            CancellationToken cancellationToken) =>
        {
            var members = await service.GetMembersAsync(slug, page, pageSize, cancellationToken);
            return Results.Ok(members);
        })
        .RequireCommunityAdmin("slug")
        .WithName("GetCommunityMembers");

        // Members - Update Role
        group.MapPost("/members/{userId}/role", async (
            [FromRoute] string slug,
            [FromRoute] string userId,
            [FromBody] UpdateMemberRoleRequest request,
            [FromServices] ICommunityManagementService service,
            CancellationToken cancellationToken) =>
        {
            var success = await service.UpdateMemberRoleAsync(slug, userId, request, cancellationToken);
            return success ? Results.Ok() : Results.NotFound();
        })
        .RequireCommunityAdmin("slug")
        .WithName("UpdateCommunityMemberRole");

        // Spaces
        group.MapGet("/spaces", async (
            [FromRoute] string slug,
            [FromServices] ICommunityManagementService service,
            CancellationToken cancellationToken) =>
        {
            var spaces = await service.GetCommunitySpacesAsync(slug, cancellationToken);
            return Results.Ok(spaces);
        })
        .RequireCommunityAdmin("slug")
        .WithName("GetCommunitySpaces");
    }
}
