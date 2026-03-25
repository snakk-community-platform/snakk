using Microsoft.AspNetCore.Mvc;
using Snakk.Api.Authorization;
using Snakk.Api.Services;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;

namespace Snakk.Api.Endpoints;

public static class GroupEndpoints
{
    public static void MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/communities/{communityId}/manage/groups")
            .WithTags("Community Groups")
            .RequireAuthorization();

        // List groups
        group.MapGet("/", async (
            [FromRoute] string communityId,
            [FromServices] IGroupService service,
            CancellationToken cancellationToken) =>
        {
            var groups = await service.GetGroupsAsync(communityId, cancellationToken);
            return Results.Ok(groups);
        })
        .RequireCommunityAdmin("communityId")
        .WithName("GetCommunityGroups");

        // Get single group
        group.MapGet("/{groupId}", async (
            [FromRoute] string communityId,
            [FromRoute] string groupId,
            [FromServices] IGroupService service,
            CancellationToken cancellationToken) =>
        {
            var g = await service.GetGroupAsync(communityId, groupId, cancellationToken);
            return g is not null ? Results.Ok(g) : Results.NotFound();
        })
        .RequireCommunityAdmin("communityId")
        .WithName("GetCommunityGroup");

        // Create group
        group.MapPost("/", async (
            [FromRoute] string communityId,
            [FromBody] CreateGroupRequest request,
            [FromServices] IGroupService service,
            [FromServices] ICurrentUserService currentUser,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUser.GetCurrentUserId() ?? string.Empty;
            var g = await service.CreateGroupAsync(communityId, request, userId, cancellationToken);
            return Results.Created($"/communities/{communityId}/manage/groups/{g.PublicId}", g);
        })
        .RequireCommunityAdmin("communityId")
        .WithName("CreateCommunityGroup");

        // Update group
        group.MapPut("/{groupId}", async (
            [FromRoute] string communityId,
            [FromRoute] string groupId,
            [FromBody] UpdateGroupRequest request,
            [FromServices] IGroupService service,
            CancellationToken cancellationToken) =>
        {
            var g = await service.UpdateGroupAsync(communityId, groupId, request, cancellationToken);
            return g is not null ? Results.Ok(g) : Results.NotFound();
        })
        .RequireCommunityAdmin("communityId")
        .WithName("UpdateCommunityGroup");

        // Delete group
        group.MapDelete("/{groupId}", async (
            [FromRoute] string communityId,
            [FromRoute] string groupId,
            [FromServices] IGroupService service,
            CancellationToken cancellationToken) =>
        {
            var success = await service.DeleteGroupAsync(communityId, groupId, cancellationToken);
            return success ? Results.NoContent() : Results.NotFound();
        })
        .RequireCommunityAdmin("communityId")
        .WithName("DeleteCommunityGroup");

        // List members
        group.MapGet("/{groupId}/members", async (
            [FromRoute] string communityId,
            [FromRoute] string groupId,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromServices] IGroupService service,
            CancellationToken cancellationToken) =>
        {
            pageSize = Math.Clamp(pageSize > 0 ? pageSize : 50, 1, 100);
            page = Math.Max(1, page);
            var members = await service.GetMembersAsync(communityId, groupId, page, pageSize, cancellationToken);
            return Results.Ok(members);
        })
        .RequireCommunityAdmin("communityId")
        .WithName("GetCommunityGroupMembers");

        // Add member
        group.MapPost("/{groupId}/members", async (
            [FromRoute] string communityId,
            [FromRoute] string groupId,
            [FromBody] AddGroupMemberRequest request,
            [FromServices] IGroupService service,
            [FromServices] ICurrentUserService currentUser,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUser.GetCurrentUserId() ?? string.Empty;
            var success = await service.AddMemberAsync(communityId, groupId, request, userId, cancellationToken);
            return success ? Results.Ok() : Results.NotFound();
        })
        .RequireCommunityAdmin("communityId")
        .WithName("AddCommunityGroupMember");

        // Remove member
        group.MapDelete("/{groupId}/members/{userId}", async (
            [FromRoute] string communityId,
            [FromRoute] string groupId,
            [FromRoute] string userId,
            [FromServices] IGroupService service,
            CancellationToken cancellationToken) =>
        {
            var success = await service.RemoveMemberAsync(communityId, groupId, userId, cancellationToken);
            return success ? Results.NoContent() : Results.NotFound();
        })
        .RequireCommunityAdmin("communityId")
        .WithName("RemoveCommunityGroupMember");
    }
}
