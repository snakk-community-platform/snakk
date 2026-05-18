namespace Snakk.Api.Endpoints;

using System.Security.Cryptography;
using System.Text;
using Snakk.Application.Services;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Repositories;

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
        IDiscussionRepository discussionRepository,
        IHubRepository hubRepository,
        ISpaceRepository spaceRepository,
        Snakk.Domain.Repositories.ICommunityRepository communityRepository,
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
                request.UserId, request.ScopeId, discussionRepository, restricted, grantsCache, ct),
            "Space" => await CheckSpaceAccessAsync(
                request.UserId, request.ScopeId, spaceRepository, restricted, grantsCache, ct),
            "Hub" => await CheckHubAccessAsync(
                request.UserId, request.ScopeId, hubRepository, restricted, grantsCache, ct),
            "Community" => await CheckCommunityAccessAsync(
                request.ScopeId, communityRepository, ct),
            _ => false
        };

        return hasAccess ? Results.Ok() : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static async Task<bool> CheckDiscussionAccessAsync(
        string userId, string publicId,
        IDiscussionRepository repo, RestrictedEntitySet restricted, IUserGrantsCacheService grantsCache,
        CancellationToken ct = default)
    {
        var discussion = await repo.GetByPublicIdAsync(publicId, ct);
        if (discussion is null) return false;

        if (!restricted.SpaceIds.Contains(discussion.SpaceId)) return true;

        var grants = await grantsCache.GetGrantsAsync(userId, ct);
        return grants.SpaceIds.Contains(discussion.SpaceId);
    }

    private static async Task<bool> CheckSpaceAccessAsync(
        string userId, string publicId,
        ISpaceRepository repo, RestrictedEntitySet restricted, IUserGrantsCacheService grantsCache,
        CancellationToken ct = default)
    {
        var space = await repo.GetByPublicIdAsync(publicId, ct);
        if (space is null) return false;

        if (!restricted.SpaceIds.Contains(space.Id)) return true;

        var grants = await grantsCache.GetGrantsAsync(userId, ct);
        return grants.SpaceIds.Contains(space.Id);
    }

    private static async Task<bool> CheckHubAccessAsync(
        string userId, string publicId,
        IHubRepository repo, RestrictedEntitySet restricted, IUserGrantsCacheService grantsCache,
        CancellationToken ct = default)
    {
        var hub = await repo.GetByPublicIdAsync(publicId, ct);
        if (hub is null) return false;

        if (!restricted.HubIds.Contains(hub.Id)) return true;

        var grants = await grantsCache.GetGrantsAsync(userId, ct);
        return grants.HubIds.Contains(hub.Id);
    }

    private static async Task<bool> CheckCommunityAccessAsync(string publicId, Snakk.Domain.Repositories.ICommunityRepository repo, CancellationToken ct = default)
    {
        var community = await repo.GetByPublicIdAsync(CommunityId.From(publicId), ct);
        return community is not null;
    }
}

public record VerifySubscriptionRequest(string UserId, string ScopeType, string ScopeId);
