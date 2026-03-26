using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class AllowedTypesService(SnakkDbContext context) : IAllowedTypesService
{
    private static readonly List<DiscussionTypeEnum> AllTypes =
        Enum.GetValues<DiscussionTypeEnum>().ToList();

    public async Task<List<DiscussionTypeEnum>> GetCommunityAllowedTypesAsync(string communityPublicId)
    {
        var types = await context.CommunityAllowedDiscussionTypes
            .Where(x => x.Community.PublicId == communityPublicId && !x.Community.IsDeleted)
            .Select(x => (DiscussionTypeEnum)x.DiscussionType)
            .ToListAsync();

        return types.Count == 0 ? AllTypes : types;
    }

    public async Task<List<DiscussionTypeEnum>> GetHubEffectiveAllowedTypesAsync(string hubPublicId)
    {
        // Get hub's own allowed types
        var hubTypes = await context.HubAllowedDiscussionTypes
            .Where(x => x.Hub.PublicId == hubPublicId && !x.Hub.IsDeleted)
            .Select(x => (DiscussionTypeEnum)x.DiscussionType)
            .ToListAsync();

        // Get parent community's allowed types
        var communityTypes = await context.Hubs
            .Where(h => h.PublicId == hubPublicId && !h.IsDeleted)
            .SelectMany(h => h.Community.AllowedDiscussionTypes)
            .Select(x => (DiscussionTypeEnum)x.DiscussionType)
            .ToListAsync();

        var effectiveHub = hubTypes.Count == 0 ? AllTypes : hubTypes;
        var effectiveCommunity = communityTypes.Count == 0 ? AllTypes : communityTypes;

        return effectiveHub.Intersect(effectiveCommunity).ToList();
    }

    public async Task<List<DiscussionTypeEnum>> GetSpaceEffectiveAllowedTypesAsync(string spacePublicId)
    {
        // Get space's own allowed types
        var spaceTypes = await context.SpaceAllowedDiscussionTypes
            .Where(x => x.Space.PublicId == spacePublicId && !x.Space.IsDeleted)
            .Select(x => (DiscussionTypeEnum)x.DiscussionType)
            .ToListAsync();

        // Get parent hub's PublicId to compute hub's effective types
        var hubPublicId = await context.Spaces
            .Where(s => s.PublicId == spacePublicId && !s.IsDeleted)
            .Select(s => s.Hub.PublicId)
            .FirstOrDefaultAsync();

        if (hubPublicId is null)
            return spaceTypes.Count == 0 ? AllTypes : spaceTypes;

        var hubEffective = await GetHubEffectiveAllowedTypesAsync(hubPublicId);
        var effectiveSpace = spaceTypes.Count == 0 ? AllTypes : spaceTypes;

        return effectiveSpace.Intersect(hubEffective).ToList();
    }

    public async Task<List<DiscussionTypeEnum>> GetParentAllowedTypesForHubAsync(string hubPublicId)
    {
        var communityPublicId = await context.Hubs
            .Where(h => h.PublicId == hubPublicId && !h.IsDeleted)
            .Select(h => h.Community.PublicId)
            .FirstOrDefaultAsync();

        if (communityPublicId is null)
            return AllTypes;

        return await GetCommunityAllowedTypesAsync(communityPublicId);
    }

    public async Task<List<DiscussionTypeEnum>> GetParentAllowedTypesForSpaceAsync(string spacePublicId)
    {
        var hubPublicId = await context.Spaces
            .Where(s => s.PublicId == spacePublicId && !s.IsDeleted)
            .Select(s => s.Hub.PublicId)
            .FirstOrDefaultAsync();

        if (hubPublicId is null)
            return AllTypes;

        return await GetHubEffectiveAllowedTypesAsync(hubPublicId);
    }
}
