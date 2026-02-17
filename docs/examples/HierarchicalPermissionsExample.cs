using Microsoft.AspNetCore.Mvc;
using Snakk.Api.Authorization;

namespace Snakk.Api.Examples;

/// <summary>
/// Example endpoints demonstrating hierarchical permission checking
/// </summary>
public static class HierarchicalPermissionsExample
{
    public static void MapExampleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/examples/permissions")
            .WithTags("Examples - Permissions");

        // ============================================================
        // EXAMPLE 1: Global Admin Only
        // ============================================================
        // Only users with GlobalAdmin role can access this
        group.MapGet("/global-admin-only", () =>
            Results.Ok(new { message = "You are a Global Admin!" }))
            .RequireGlobalAdmin()
            .WithName("GlobalAdminExample");

        // ============================================================
        // EXAMPLE 2: Community Admin (with hierarchy)
        // ============================================================
        // Users can access if they are:
        // - GlobalAdmin (anywhere), OR
        // - CommunityAdmin/CommunityMod for the specified community
        group.MapGet("/community/{communityId}/settings",
            ([FromRoute] int communityId) =>
                Results.Ok(new { message = $"Managing community {communityId}" }))
            .RequireCommunityAdmin() // Uses "communityId" route parameter
            .WithName("CommunityAdminExample");

        // ============================================================
        // EXAMPLE 3: Hub Moderator (with hierarchy)
        // ============================================================
        // Users can access if they are:
        // - GlobalAdmin (anywhere), OR
        // - CommunityAdmin/CommunityMod for the parent community, OR
        // - HubMod for this specific hub
        group.MapGet("/hub/{hubId}/moderate",
            ([FromRoute] int hubId) =>
                Results.Ok(new { message = $"Moderating hub {hubId}" }))
            .RequireHubModerator() // Uses "hubId" route parameter
            .WithName("HubModeratorExample");

        // ============================================================
        // EXAMPLE 4: Space Moderator (with full hierarchy)
        // ============================================================
        // Users can access if they are:
        // - GlobalAdmin (anywhere), OR
        // - CommunityAdmin/CommunityMod for the parent community, OR
        // - HubMod for the parent hub, OR
        // - SpaceMod for this specific space
        group.MapGet("/space/{spaceId}/moderate",
            ([FromRoute] int spaceId) =>
                Results.Ok(new { message = $"Moderating space {spaceId}" }))
            .RequireSpaceModerator() // Uses "spaceId" route parameter
            .WithName("SpaceModeratorExample");

        // ============================================================
        // EXAMPLE 5: Discussion Moderation (full hierarchy)
        // ============================================================
        // Users can access if they have moderation rights at any level
        // of the hierarchy above this discussion
        group.MapDelete("/discussion/{discussionId}",
            ([FromRoute] int discussionId) =>
                Results.Ok(new { message = $"Deleted discussion {discussionId}" }))
            .RequireAnyModerator("Discussion", "discussionId")
            .WithName("DeleteDiscussionExample");

        // ============================================================
        // EXAMPLE 6: Custom Permission with Custom Route Parameter
        // ============================================================
        // For cases where the route parameter name is different
        group.MapPost("/communities/{id}/ban-user",
            ([FromRoute] int id) =>
                Results.Ok(new { message = $"Banned user in community {id}" }))
            .RequirePermission("BanUser", "Community", "id") // Custom route key
            .WithName("BanUserExample");
    }
}

/*
============================================================
HOW THE HIERARCHY WORKS
============================================================

Example Scenario:
- User "Alice" is CommunityAdmin for Community #5
- Community #5 contains Hub #12
- Hub #12 contains Space #42
- Space #42 contains Discussion #100

Access Matrix:
┌─────────────────────┬──────────┬────────────┬──────┬───────┐
│ Endpoint            │ Global   │ Community  │ Hub  │ Space │
│                     │ Admin    │ Admin (#5) │ Mod  │ Mod   │
├─────────────────────┼──────────┼────────────┼──────┼───────┤
│ /global-admin-only  │    ✅    │     ❌     │  ❌  │  ❌   │
│ /community/5/...    │    ✅    │     ✅     │  ❌  │  ❌   │
│ /community/99/...   │    ✅    │     ❌     │  ❌  │  ❌   │
│ /hub/12/...         │    ✅    │     ✅     │  ✅  │  ❌   │
│ /hub/99/...         │    ✅    │     ❌     │  ❌  │  ❌   │
│ /space/42/...       │    ✅    │     ✅     │  ✅  │  ✅   │
│ /space/99/...       │    ✅    │     ❌     │  ❌  │  ❌   │
│ /discussion/100/... │    ✅    │     ✅     │  ✅  │  ✅   │
└─────────────────────┴──────────┴────────────┴──────┴───────┘

Key Points:
1. GlobalAdmin can access EVERYTHING
2. CommunityAdmin can access their community + all child hubs/spaces/discussions
3. HubMod can access their hub + all child spaces/discussions
4. SpaceMod can ONLY access their specific space + discussions within it
5. Permissions "bubble down" the hierarchy but NOT up

============================================================
TESTING THE HIERARCHY
============================================================

# Test 1: Global Admin
curl -H "Authorization: Bearer <global-admin-token>" \
  http://localhost:5000/api/examples/permissions/space/42/moderate
# Expected: 200 OK (GlobalAdmin has access everywhere)

# Test 2: Community Admin
curl -H "Authorization: Bearer <community-admin-token>" \
  http://localhost:5000/api/examples/permissions/space/42/moderate
# Expected: 200 OK (if Community #5 contains Space #42)
# Expected: 403 Forbidden (if different community)

# Test 3: Space Mod
curl -H "Authorization: Bearer <space-mod-token>" \
  http://localhost:5000/api/examples/permissions/hub/12/moderate
# Expected: 403 Forbidden (SpaceMod cannot access parent Hub)

# Test 4: Regular User
curl -H "Authorization: Bearer <user-token>" \
  http://localhost:5000/api/examples/permissions/community/5/settings
# Expected: 403 Forbidden (no moderator role)

*/
