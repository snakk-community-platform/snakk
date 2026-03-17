using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Infrastructure.Services;

public class RuleService(SnakkDbContext context, HybridCache cache) : IRuleService
{
    private const string SiteRulesRevisionCacheKey = "site-rules-revision";
    private static readonly HybridCacheEntryOptions RevisionCacheOptions = new() { Expiration = TimeSpan.FromHours(1) };
    public async Task<RulesDto> GetRulesAsync(
        string scopeType,
        string? scopePublicId,
        CancellationToken cancellationToken = default)
    {
        var query = scopeType switch
        {
            "Site" => context.Rules
                .Where(r => r.CommunityId == null && r.HubId == null && r.SpaceId == null),
            "Community" => context.Rules
                .Where(r =>
                    r.Community!.PublicId == scopePublicId
                    && r.HubId == null
                    && r.SpaceId == null),
            "Hub" => context.Rules
                .Where(r =>
                    r.Hub!.PublicId == scopePublicId
                    && r.SpaceId == null),
            "Space" => context.Rules
                .Where(r => r.Space!.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        var rules = await query
            .OrderBy(r => r.SortOrder)
            .Select(r => new RuleDto
            {
                Title = r.Title,
                Description = r.Description,
                Order = r.SortOrder
            })
            .ToListAsync(cancellationToken);

        return new RulesDto { Rules = rules };
    }

    public async Task<RulesDto> UpdateRulesAsync(
        string scopeType,
        string? scopePublicId,
        UpdateRulesRequest request,
        CancellationToken cancellationToken = default)
    {
        switch (scopeType)
        {
            case "Site":
                await UpdateSiteRulesAsync(request, cancellationToken);
                break;
            case "Community":
                await UpdateCommunityRulesAsync(scopePublicId!, request, cancellationToken);
                break;
            case "Hub":
                await UpdateHubRulesAsync(scopePublicId!, request, cancellationToken);
                break;
            case "Space":
                await UpdateSpaceRulesAsync(scopePublicId!, request, cancellationToken);
                break;
            default:
                throw new ArgumentException($"Unknown scope type: {scopeType}");
        }

        return await GetRulesAsync(scopeType, scopePublicId, cancellationToken);
    }

    public async Task<bool> HasSiteRulesAsync(CancellationToken cancellationToken = default) =>
        await context.Rules
            .AnyAsync(
                r => r.CommunityId == null && r.HubId == null && r.SpaceId == null,
                cancellationToken);

    public async Task<string> GetSiteRulesRevisionAsync(CancellationToken cancellationToken = default) =>
        await cache.GetOrCreateAsync(
            SiteRulesRevisionCacheKey,
            async cancel =>
            {
                var setting = await context.SystemSettings
                    .Where(s => s.Category == "Rules" && s.Key == "SiteRulesRevision")
                    .Select(s => s.Value)
                    .FirstOrDefaultAsync(cancel);
                return setting ?? "";
            },
            RevisionCacheOptions,
            cancellationToken: cancellationToken);

    private async Task UpdateSiteRulesAsync(
        UpdateRulesRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await context.Rules
            .Where(r => r.CommunityId == null && r.HubId == null && r.SpaceId == null)
            .ToListAsync(cancellationToken);

        context.Rules.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var newRules = request.Rules
            .Select((r, index) => new RuleDatabaseEntity
            {
                Title = r.Title,
                Description = r.Description,
                SortOrder = index,
                CreatedAt = now
            })
            .ToList();

        context.Rules.AddRange(newRules);

        // Bump site rules revision
        var revisionSetting = await context.SystemSettings
            .FirstOrDefaultAsync(
                s => s.Category == "Rules" && s.Key == "SiteRulesRevision",
                cancellationToken);

        var newRevision = Guid.NewGuid().ToString("N")[..8];
        if (revisionSetting is not null)
        {
            revisionSetting.Value = newRevision;
            revisionSetting.UpdatedAt = now;
        }
        else
        {
            context.SystemSettings.Add(new SystemSettingDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                Category = "Rules",
                Key = "SiteRulesRevision",
                Value = newRevision,
                ValueType = "String",
                Description = "Cache-busting revision for site-wide rules",
                CreatedAt = now
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(SiteRulesRevisionCacheKey, cancellationToken);
    }

    private async Task UpdateCommunityRulesAsync(
        string communityPublicId,
        UpdateRulesRequest request,
        CancellationToken cancellationToken)
    {
        var community = await context.Communities
            .AsTracking()
            .FirstOrDefaultAsync(c => c.PublicId == communityPublicId, cancellationToken);

        if (community is null) return;

        var existing = await context.Rules
            .Where(r => r.CommunityId == community.Id && r.HubId == null && r.SpaceId == null)
            .ToListAsync(cancellationToken);

        context.Rules.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var newRules = request.Rules
            .Select((r, index) => new RuleDatabaseEntity
            {
                CommunityId = community.Id,
                Title = r.Title,
                Description = r.Description,
                SortOrder = index,
                CreatedAt = now
            })
            .ToList();

        context.Rules.AddRange(newRules);

        // Update denormalized fields
        var hasRules = newRules.Count > 0;
        community.HasRules = hasRules;
        community.RulesRevision = Guid.NewGuid().ToString("N")[..8];

        // Cascade: update ParentCommunityHasRules on all child hubs
        var hubIds = await context.Hubs
            .Where(h => h.CommunityId == community.Id)
            .Select(h => h.Id)
            .ToListAsync(cancellationToken);

        await context.Hubs
            .Where(h => h.CommunityId == community.Id)
            .ExecuteUpdateAsync(
                h => h.SetProperty(x => x.ParentCommunityHasRules, hasRules),
                cancellationToken);

        // Cascade: update ParentCommunityHasRules on all child spaces (through hubs)
        await context.Spaces
            .Where(s => hubIds.Contains(s.HubId))
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.ParentCommunityHasRules, hasRules),
                cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateHubRulesAsync(
        string hubPublicId,
        UpdateRulesRequest request,
        CancellationToken cancellationToken)
    {
        var hub = await context.Hubs
            .AsTracking()
            .FirstOrDefaultAsync(h => h.PublicId == hubPublicId, cancellationToken);

        if (hub is null) return;

        var existing = await context.Rules
            .Where(r => r.HubId == hub.Id && r.SpaceId == null)
            .ToListAsync(cancellationToken);

        context.Rules.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var newRules = request.Rules
            .Select((r, index) => new RuleDatabaseEntity
            {
                HubId = hub.Id,
                Title = r.Title,
                Description = r.Description,
                SortOrder = index,
                CreatedAt = now
            })
            .ToList();

        context.Rules.AddRange(newRules);

        // Update denormalized fields
        hub.HasRules = newRules.Count > 0;
        hub.RulesRevision = Guid.NewGuid().ToString("N")[..8];

        // Cascade: update ParentHubHasRules on all child spaces
        await context.Spaces
            .Where(s => s.HubId == hub.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.ParentHubHasRules, newRules.Count > 0),
                cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateSpaceRulesAsync(
        string spacePublicId,
        UpdateRulesRequest request,
        CancellationToken cancellationToken)
    {
        var space = await context.Spaces
            .AsTracking()
            .FirstOrDefaultAsync(s => s.PublicId == spacePublicId, cancellationToken);

        if (space is null) return;

        var existing = await context.Rules
            .Where(r => r.SpaceId == space.Id)
            .ToListAsync(cancellationToken);

        context.Rules.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var newRules = request.Rules
            .Select((r, index) => new RuleDatabaseEntity
            {
                SpaceId = space.Id,
                Title = r.Title,
                Description = r.Description,
                SortOrder = index,
                CreatedAt = now
            })
            .ToList();

        context.Rules.AddRange(newRules);

        // Update denormalized fields
        space.HasRules = newRules.Count > 0;
        space.RulesRevision = Guid.NewGuid().ToString("N")[..8];

        await context.SaveChangesAsync(cancellationToken);
    }
}
