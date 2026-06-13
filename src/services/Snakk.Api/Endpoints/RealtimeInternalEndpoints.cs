namespace Snakk.Api.Endpoints;

using System.Security.Cryptography;
using System.Text;
using Snakk.Application.Services;

public static class RealtimeInternalEndpoints
{
    public static void MapRealtimeInternalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/internal/realtime/verify-subscription", VerifySubscriptionAsync)
            .ExcludeFromDescription();
    }

    private static async Task<IResult> VerifySubscriptionAsync(
        VerifySubscriptionRequest request,
        IConfiguration configuration,
        IEntityHierarchyCacheService hierarchyCache,
        IUserGrantsCacheService grantsCache,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Validate API key with constant-time comparison (mirrors ApiKeyAuthMiddleware in Snakk.Realtime)
        var expectedKey = configuration["Realtime:ApiKey"] ?? "";
        if (!httpContext.Request.Headers.TryGetValue("X-Api-Key", out var key))
            return Results.StatusCode(StatusCodes.Status401Unauthorized);

        var keyBytes      = Encoding.UTF8.GetBytes(key.ToString());
        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        if (!CryptographicOperations.FixedTimeEquals(keyBytes, expectedBytes))
            return Results.StatusCode(StatusCodes.Status401Unauthorized);

        // Platform-wide restricted-entity set (cached, ~30 s TTL) used as a short-circuit
        var restricted = await grantsCache.GetRestrictedEntitiesAsync(ct);

        var hasAccess = request.ScopeType switch
        {
            "Discussion" => await CheckDiscussionAccessAsync(
                request.UserId, request.ScopeId, hierarchyCache, restricted, grantsCache, ct),
            "Space" => await CheckSpaceAccessAsync(
                request.UserId, request.ScopeId, hierarchyCache, restricted, grantsCache, ct),
            "Hub" => await CheckHubAccessAsync(
                request.UserId, request.ScopeId, hierarchyCache, restricted, grantsCache, ct),
            "Community" => await CheckCommunityAccessAsync(
                request.ScopeId, hierarchyCache, ct),
            _ => false
        };

        return hasAccess ? Results.Ok() : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static async Task<bool> CheckDiscussionAccessAsync(
        string userId, string publicId,
        IEntityHierarchyCacheService hierarchyCache,
        RestrictedEntitySet restricted,
        IUserGrantsCacheService grantsCache,
        CancellationToken ct = default)
    {
        var hierarchy = await hierarchyCache.GetDiscussionHierarchyAsync(publicId, ct);
        if (hierarchy is null) return false;

        if (!restricted.SpaceIds.Contains(hierarchy.SpaceId)) return true;

        var grants = await grantsCache.GetGrantsAsync(userId, ct);
        return grants.SpaceIds.Contains(hierarchy.SpaceId);
    }

    private static async Task<bool> CheckSpaceAccessAsync(
        string userId, string publicId,
        IEntityHierarchyCacheService hierarchyCache,
        RestrictedEntitySet restricted,
        IUserGrantsCacheService grantsCache,
        CancellationToken ct = default)
    {
        var hierarchy = await hierarchyCache.GetSpaceHierarchyAsync(publicId, ct);
        if (hierarchy is null) return false;

        if (!restricted.SpaceIds.Contains(hierarchy.Id)) return true;

        var grants = await grantsCache.GetGrantsAsync(userId, ct);
        return grants.SpaceIds.Contains(hierarchy.Id);
    }

    private static async Task<bool> CheckHubAccessAsync(
        string userId, string publicId,
        IEntityHierarchyCacheService hierarchyCache,
        RestrictedEntitySet restricted,
        IUserGrantsCacheService grantsCache,
        CancellationToken ct = default)
    {
        var hierarchy = await hierarchyCache.GetHubHierarchyAsync(publicId, ct);
        if (hierarchy is null) return false;

        if (!restricted.HubIds.Contains(hierarchy.Id)) return true;

        var grants = await grantsCache.GetGrantsAsync(userId, ct);
        return grants.HubIds.Contains(hierarchy.Id);
    }

    private static async Task<bool> CheckCommunityAccessAsync(
        string publicId,
        IEntityHierarchyCacheService hierarchyCache,
        CancellationToken ct = default)
    {
        var communityId = await hierarchyCache.GetCommunityIdAsync(publicId, ct);
        return communityId is not null;
    }
}

public record VerifySubscriptionRequest(string UserId, string ScopeType, string ScopeId);
