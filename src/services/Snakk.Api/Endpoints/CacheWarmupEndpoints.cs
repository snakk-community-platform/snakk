namespace Snakk.Api.Endpoints;

using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Repositories;

public static class CacheWarmupEndpoints
{
    public static void MapCacheWarmupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/internal/cache/warm", WarmCachesAsync)
            .ExcludeFromDescription();
    }

    private static IResult WarmCachesAsync(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _ = DoWarmAsync(scopeFactory, logger);
        return Results.Accepted();
    }

    private static async Task DoWarmAsync(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sp = scope.ServiceProvider;

            // Sequential calls — all services in the scope share one DbContext instance;
            // concurrent access to EF Core DbContext is not safe.

            // --- Global caches ---
            await sp.GetRequiredService<ISettingsService>().GetSiteInfoAsync();
            await sp.GetRequiredService<IPermissionService>().GetAllPermissionsAsync();
            await sp.GetRequiredService<IManageScopeDataService>().GetGlobalAdminPublicIdsAsync();
            var grants = sp.GetRequiredService<IUserGrantsCacheService>();
            await grants.GetRestrictedEntitiesAsync();
            await grants.GetAdultHidingSpaceIdsAsync();
            var rules = sp.GetRequiredService<IRuleService>();
            await rules.HasSiteRulesAsync();
            await rules.GetSiteRulesRevisionAsync();
            await rules.GetRulesAsync("Site", null);

            // --- Enumerate entity public IDs ---
            // Communities: page through public-listed entries (all visibility = public)
            var communityUseCase = sp.GetRequiredService<CommunityUseCase>();
            var communityIds = new List<string>();
            var hasMore = true;
            var offset = 0;
            const int pageSize = 100;
            while (hasMore)
            {
                var page = await communityUseCase.GetPublicCommunitiesAsync(offset, pageSize);
                communityIds.AddRange(page.Items.Select(c => c.PublicId.Value));
                hasMore = page.HasMoreItems;
                offset += pageSize;
            }

            // GetAllAsync is capped at 1000 rows — sufficient for typical installs including seeded data
            var hubIds = (await sp.GetRequiredService<IHubRepository>().GetAllAsync())
                .Select(h => h.PublicId.Value).ToList();
            var spaceIds = (await sp.GetRequiredService<ISpaceRepository>().GetAllAsync())
                .Select(s => s.PublicId.Value).ToList();

            // --- Per-entity caches ---
            var communityData = sp.GetRequiredService<ICommunityDataService>();
            var hubData = sp.GetRequiredService<IHubDataService>();
            var spaceData = sp.GetRequiredService<ISpaceDataService>();
            var hierarchy = sp.GetRequiredService<IEntityHierarchyCacheService>();

            foreach (var id in communityIds)
            {
                await communityData.GetCommunityMetaAsync(id);
                await hierarchy.GetCommunityIdAsync(id);
                await rules.GetRulesAsync("Community", id);
            }

            foreach (var id in hubIds)
            {
                await hubData.GetHubMetaAsync(id);
                await hierarchy.GetHubHierarchyAsync(id);
                await rules.GetRulesAsync("Hub", id);
            }

            foreach (var id in spaceIds)
            {
                await spaceData.GetSpaceMetaAsync(id);
                await hierarchy.GetSpaceHierarchyAsync(id);
                await rules.GetRulesAsync("Space", id);
            }

            logger.LogInformation(
                "[CacheWarmup] done — {Communities} communities, {Hubs} hubs, {Spaces} spaces",
                communityIds.Count, hubIds.Count, spaceIds.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CacheWarmup] failed");
        }
    }
}
