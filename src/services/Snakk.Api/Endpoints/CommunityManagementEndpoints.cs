using Microsoft.AspNetCore.Mvc;
using Snakk.Api.Authorization;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;

namespace Snakk.Api.Endpoints;

public static class CommunityManagementEndpoints
{
    public static void MapCommunityManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/communities/{communityId}/manage")
            .WithTags("Community Management")
            .RequireAuthorization();

        // Overview
        group.MapGet("/overview", async (
            [FromRoute] string communityId,
            [FromServices] ICommunityManagementService service,
            CancellationToken cancellationToken) =>
        {
            var overview = await service.GetOverviewAsync(communityId, cancellationToken);
            return overview is not null ? Results.Ok(overview) : Results.NotFound();
        })
        .RequireCommunityAdmin("communityId")
        .WithName("GetCommunityOverview");

        // Settings - Get
        group.MapGet("/settings", async (
            [FromRoute] string communityId,
            [FromServices] ICommunityManagementService service,
            CancellationToken cancellationToken) =>
        {
            var settings = await service.GetSettingsAsync(communityId, cancellationToken);
            return settings is not null ? Results.Ok(settings) : Results.NotFound();
        })
        .RequireCommunityAdmin("communityId")
        .WithName("GetCommunitySettings");

        // Settings - Update
        group.MapPut("/settings", async (
            [FromRoute] string communityId,
            [FromBody] UpdateCommunitySettingsRequest request,
            [FromServices] ICommunityManagementService service,
            CancellationToken cancellationToken) =>
        {
            var settings = await service.UpdateSettingsAsync(communityId, request, cancellationToken);
            return settings is not null ? Results.Ok(settings) : Results.NotFound();
        })
        .RequireCommunityAdmin("communityId")
        .WithName("UpdateCommunitySettings");

        // Moderation
        group.MapGet("/moderation", async (
            [FromRoute] string communityId,
            [FromServices] ICommunityManagementService service,
            CancellationToken cancellationToken) =>
        {
            var moderation = await service.GetModerationDataAsync(communityId, cancellationToken);
            return Results.Ok(moderation);
        })
        .RequireCommunityAdmin("communityId")
        .WithName("GetCommunityModeration");

        // Members - List
        group.MapGet("/members", async (
            [FromRoute] string communityId,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromServices] ICommunityManagementService service,
            CancellationToken cancellationToken) =>
        {
            // Clamp pagination parameters
            pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);
            page = Math.Max(1, page);

            var members = await service.GetMembersAsync(communityId, page, pageSize, cancellationToken);
            return Results.Ok(members);
        })
        .RequireCommunityAdmin("communityId")
        .WithName("GetCommunityMembers");

        // Members - Update Role
        group.MapPost("/members/{userId}/role", async (
            [FromRoute] string communityId,
            [FromRoute] string userId,
            [FromBody] UpdateMemberRoleRequest request,
            [FromServices] ICommunityManagementService service,
            CancellationToken cancellationToken) =>
        {
            var success = await service.UpdateMemberRoleAsync(communityId, userId, request, cancellationToken);
            return success ? Results.Ok() : Results.NotFound();
        })
        .RequireCommunityAdmin("communityId")
        .WithName("UpdateCommunityMemberRole");

        // Spaces
        group.MapGet("/spaces", async (
            [FromRoute] string communityId,
            [FromServices] ICommunityManagementService service,
            CancellationToken cancellationToken) =>
        {
            var spaces = await service.GetCommunitySpacesAsync(communityId, cancellationToken);
            return Results.Ok(spaces);
        })
        .RequireCommunityAdmin("communityId")
        .WithName("GetCommunitySpaces");

        // Rules - Get
        group.MapGet("/rules", async (
            [FromRoute] string communityId,
            [FromServices] IRuleService ruleService,
            CancellationToken cancellationToken) =>
        {
            var rules = await ruleService.GetRulesAsync("Community", communityId, cancellationToken);
            return Results.Ok(rules);
        })
        .RequireCommunityAdmin("communityId")
        .WithName("GetCommunityRulesManagement");

        // Rules - Update
        group.MapPut("/rules", async (
            [FromRoute] string communityId,
            [FromBody] UpdateRulesRequest request,
            [FromServices] IRuleService ruleService,
            CancellationToken cancellationToken) =>
        {
            var rules = await ruleService.UpdateRulesAsync("Community", communityId, request, cancellationToken);
            return Results.Ok(rules);
        })
        .RequireCommunityAdmin("communityId")
        .WithName("UpdateCommunityRules");
    }
}
