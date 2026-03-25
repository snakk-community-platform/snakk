using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Api.Helpers;
using Snakk.Api.Services;
using Snakk.Application.Repositories;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Protos.Hub;
using Snakk.Shared.Enums;
using Snakk.Shared.Helpers;

namespace Snakk.Api.GrpcServices;

public class HubGrpcService(
    HubUseCase hubUseCase,
    ISearchRepository searchRepository,
    IRuleService ruleService,
    StatisticsUseCase statisticsUseCase,
    SnakkDbContext dbContext,
    ICurrentUserService currentUser,
    IUserGrantsCacheService grantsCache,
    IEntityHierarchyCacheService hierarchyCache,
    HybridCache cache) : HubService.HubServiceBase
{
    public override async Task<HubInfo> GetHubBySlug(GetHubBySlugRequest request, ServerCallContext context)
    {
        var result = await hubUseCase.GetHubBySlugAsync(request.Slug, request.CommunitySlug);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Hub not found"));

        if (!await IsHubAccessibleAsync(result.Value.PublicId.Value, currentUser.GetCurrentUserId()))
            throw new RpcException(new Status(StatusCode.NotFound, "Hub not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value);

        return info;
    }

    public override async Task<HubInfo> GetHub(GetHubRequest request, ServerCallContext context)
    {
        var result = await hubUseCase.GetHubAsync(HubId.From(request.PublicId));

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Hub not found"));

        if (!await IsHubAccessibleAsync(result.Value.PublicId.Value, currentUser.GetCurrentUserId()))
            throw new RpcException(new Status(StatusCode.NotFound, "Hub not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value);

        return info;
    }

    public override async Task<PagedHubList> ListHubs(ListHubsRequest request, ServerCallContext context)
    {
        var userId = currentUser.GetCurrentUserId();
        var result = await searchRepository.GetHubsAsync(request.Offset, request.PageSize, userId);

        var response = new PagedHubList
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var h in result.Items)
        {
            response.Items.Add(new HubInfo
            {
                PublicId = h.PublicId,
                CommunityId = h.CommunityPublicId,
                Name = h.Name,
                Slug = h.Slug,
                Description = h.Description ?? "",
                SpaceCount = h.SpaceCount,
                DiscussionCount = h.DiscussionCount,
                ReplyCount = h.ReplyCount,

                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(h.CreatedAt, DateTimeKind.Utc))
            });
        }

        return response;
    }

    public override async Task<PagedHubList> ListHubsByCommunity(ListHubsByCommunityRequest request, ServerCallContext context)
    {
        var userId = currentUser.GetCurrentUserId();
        var result = await hubUseCase.GetHubsByCommunityAsync(
            CommunityId.From(request.CommunityId),
            request.Offset,
            request.PageSize,
            userId);

        var publicIds = result.Items.Select(h => h.PublicId.Value).ToList();
        var counts = await dbContext.Hubs
            .Where(h => publicIds.Contains(h.PublicId))
            .Select(h => new { h.PublicId, h.SpaceCount, h.DiscussionCount, h.PostCount })
            .ToDictionaryAsync(h => h.PublicId);

        var response = new PagedHubList
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var h in result.Items)
        {
            var info = MapToProto(h);
            if (counts.TryGetValue(h.PublicId.Value, out var cnt))
            {
                info.SpaceCount = cnt.SpaceCount;
                info.DiscussionCount = cnt.DiscussionCount;
                info.ReplyCount = cnt.PostCount - cnt.DiscussionCount;
            }
            response.Items.Add(info);
        }

        return response;
    }

    public override async Task<HubStats> GetHubStats(GetHubStatsRequest request, ServerCallContext context)
    {
        var result = await statisticsUseCase.GetHubStatsAsync(request.PublicId);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Hub not found"));

        var stats = result.Value;

        return new HubStats
        {
            PublicId = stats.PublicId,
            Name = stats.Name,
            Description = stats.Description ?? "",
            AvatarUrl = AvatarHelper.GetAvatarUrl(stats.PublicId, AvatarEntityType.Hub, 0),
            SpaceCount = stats.SpaceCount,
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount
        };
    }

    public override async Task<HubRulesResponse> GetHubRules(GetHubRulesRequest request, ServerCallContext context)
    {
        var rules = await ruleService.GetRulesAsync("Hub", request.HubId);

        var response = new HubRulesResponse();

        foreach (var r in rules.Rules)
        {
            response.Rules.Add(new HubRule
            {
                Id = r.Order,
                Title = r.Title,
                Description = r.Description
            });
        }

        return response;
    }

    private async Task<bool> IsHubAccessibleAsync(string hubPublicId, string? userId)
    {
        var restricted = await grantsCache.GetRestrictedEntitiesAsync();
        if (restricted.IsEmpty) return true;

        var h = await hierarchyCache.GetHubHierarchyAsync(hubPublicId);

        if (h is null) return false;

        var hubGate = restricted.HubIds.Contains(h.Id);
        var communityGate = restricted.CommunityIds.Contains(h.CommunityId);

        if (!hubGate && !communityGate) return true;
        if (userId is null) return false;

        var grants = await grantsCache.GetGrantsAsync(userId);
        return (!hubGate || grants.HubIds.Contains(h.Id))
            && (!communityGate || grants.CommunityIds.Contains(h.CommunityId));
    }

    private sealed record HubMeta(bool HasRules, string? RulesRevision, bool ParentCommunityHasRules, string? TeamRevision, bool IsRestricted);
    private static readonly HybridCacheEntryOptions MetaCacheOptions = new() { Expiration = TimeSpan.FromMinutes(5) };

    private async Task PopulateRulesMetadata(HubInfo info, string publicId)
    {
        var data = await cache.GetOrCreateAsync<HubMeta?>(
            $"hub-meta:{publicId}",
            async cancel =>
            {
                var raw = await dbContext.Hubs
                    .Where(h => h.PublicId == publicId)
                    .Select(h => new { h.HasRules, h.RulesRevision, h.ParentCommunityHasRules, h.TeamRevision, h.IsRestricted })
                    .FirstOrDefaultAsync(cancel);
                return raw is null ? null : new HubMeta(raw.HasRules, raw.RulesRevision, raw.ParentCommunityHasRules, raw.TeamRevision, raw.IsRestricted);
            },
            MetaCacheOptions);

        if (data is not null)
        {
            info.HasRules = data.HasRules;
            info.RulesRevision = data.RulesRevision ?? "";
            info.ParentCommunityHasRules = data.ParentCommunityHasRules;
            info.TeamRevision = data.TeamRevision ?? "";
            info.IsRestricted = data.IsRestricted;
        }
    }

    private static HubInfo MapToProto(Snakk.Domain.Entities.Hub h)
    {
        var info = new HubInfo
        {
            PublicId = h.PublicId.Value,
            CommunityId = h.CommunityId.Value,
            Name = h.Name,
            Slug = h.Slug,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(h.CreatedAt, DateTimeKind.Utc))
        };

        if (h.Description is not null)
            info.Description = h.Description;

        return info;
    }
}
