using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Api.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Protos.Community;
using Snakk.Shared.Helpers;
using System.Security.Claims;

namespace Snakk.Api.GrpcServices;

public class CommunityGrpcService(
    CommunityUseCase communityUseCase,
    IRuleService ruleService,
    StatisticsUseCase statisticsUseCase,
    SnakkDbContext dbContext,
    IGroupAccessService groupAccessService,
    ICurrentUserService currentUser,
    IUserGrantsCacheService grantsCache,
    IEntityHierarchyCacheService hierarchyCache,
    HybridCache cache) : CommunityService.CommunityServiceBase
{
    public override async Task<CommunityInfo> GetCommunityBySlug(GetCommunityBySlugRequest request, ServerCallContext context)
    {
        var result = await communityUseCase.GetCommunityBySlugAsync(request.Slug);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        if (!await IsCommunityAccessibleAsync(result.Value.PublicId.Value, currentUser.GetCurrentUserId()))
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value);

        return info;
    }

    public override async Task<CommunityInfo> GetCommunityByDomain(GetCommunityByDomainRequest request, ServerCallContext context)
    {
        var result = await communityUseCase.GetCommunityByDomainAsync(request.Domain);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        if (!await IsCommunityAccessibleAsync(result.Value.PublicId.Value, currentUser.GetCurrentUserId()))
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value);

        return info;
    }

    public override async Task<CommunityInfo> GetCommunity(GetCommunityRequest request, ServerCallContext context)
    {
        var result = await communityUseCase.GetCommunityAsync(CommunityId.From(request.PublicId));

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        if (!await IsCommunityAccessibleAsync(result.Value.PublicId.Value, currentUser.GetCurrentUserId()))
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value);

        return info;
    }

    public override async Task<PagedCommunityList> ListCommunities(ListCommunitiesRequest request, ServerCallContext context)
    {
        var result = await communityUseCase.GetPublicCommunitiesAsync(request.Offset, request.PageSize);

        // Fetch denormalized counts from database entities
        var publicIds = result.Items.Select(c => c.PublicId.Value).ToList();
        var counts = await dbContext.Communities
            .Where(c => publicIds.Contains(c.PublicId))
            .Select(c => new { c.PublicId, c.DiscussionCount, c.PostCount })
            .ToDictionaryAsync(c => c.PublicId);

        var response = new PagedCommunityList
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var c in result.Items)
        {
            var info = MapToProto(c);
            if (counts.TryGetValue(c.PublicId.Value, out var cnt))
            {
                info.DiscussionCount = cnt.DiscussionCount;
                info.ReplyCount = cnt.PostCount;
            }
            response.Items.Add(info);
        }

        return response;
    }

    public override async Task<CommunityStats> GetCommunityStats(GetCommunityStatsRequest request, ServerCallContext context)
    {
        var result = await statisticsUseCase.GetCommunityStatsAsync(request.PublicId);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        var stats = result.Value;

        return new CommunityStats
        {
            PublicId = stats.PublicId,
            Name = stats.Name,
            Description = stats.Description ?? "",
            AvatarUrl = AvatarHelper.GetAvatarUrl(stats.PublicId, AvatarEntityType.Community, 0),
            HubCount = stats.HubCount,
            SpaceCount = stats.SpaceCount,
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount
        };
    }

    public override async Task<CommunityRulesResponse> GetCommunityRules(GetCommunityRulesRequest request, ServerCallContext context)
    {
        var rules = await ruleService.GetRulesAsync("Community", request.CommunityId);

        var response = new CommunityRulesResponse();

        foreach (var r in rules.Rules)
        {
            response.Rules.Add(new CommunityRule
            {
                Id = r.Order,
                Title = r.Title,
                Description = r.Description
            });
        }

        return response;
    }

    public override async Task<SiteRulesResponse> GetSiteRules(GetSiteRulesRequest request, ServerCallContext context)
    {
        var rules = await ruleService.GetRulesAsync("Site", null);
        var revision = await ruleService.GetSiteRulesRevisionAsync();

        var response = new SiteRulesResponse { Revision = revision };

        foreach (var r in rules.Rules)
        {
            response.Rules.Add(new SiteRule
            {
                Id = r.Order,
                Title = r.Title,
                Description = r.Description
            });
        }

        return response;
    }

    private async Task<bool> IsCommunityAccessibleAsync(string communityPublicId, string? userId)
    {
        var restricted = await grantsCache.GetRestrictedEntitiesAsync();
        if (restricted.IsEmpty) return true;

        var communityDbId = await hierarchyCache.GetCommunityIdAsync(communityPublicId);

        if (communityDbId is null) return false;
        if (!restricted.CommunityIds.Contains(communityDbId.Value)) return true;
        if (userId is null) return false;

        var grants = await grantsCache.GetGrantsAsync(userId);
        return grants.CommunityIds.Contains(communityDbId.Value);
    }

    private sealed record CommunityMeta(bool HasRules, string? RulesRevision, string? TeamRevision, bool IsRestricted);
    private static readonly HybridCacheEntryOptions MetaCacheOptions = new() { Expiration = TimeSpan.FromMinutes(5) };

    private async Task PopulateRulesMetadata(CommunityInfo info, string publicId)
    {
        var data = await cache.GetOrCreateAsync<CommunityMeta?>(
            $"community-meta:{publicId}",
            async cancel =>
            {
                var raw = await dbContext.Communities
                    .Where(c => c.PublicId == publicId)
                    .Select(c => new { c.HasRules, c.RulesRevision, c.TeamRevision, c.IsRestricted })
                    .FirstOrDefaultAsync(cancel);
                return raw is null ? null : new CommunityMeta(raw.HasRules, raw.RulesRevision, raw.TeamRevision, raw.IsRestricted);
            },
            MetaCacheOptions);

        if (data is not null)
        {
            info.HasRules = data.HasRules;
            info.RulesRevision = data.RulesRevision ?? "";
            info.TeamRevision = data.TeamRevision ?? "";
            info.IsRestricted = data.IsRestricted;
        }
    }

    public override async Task<CheckGroupAccessResponse> CheckGroupAccess(
        CheckGroupAccessRequest request,
        ServerCallContext context)
    {
        var userPublicId = context.GetHttpContext().User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        var result = await groupAccessService.CheckAccessAsync(
            userPublicId,
            request.CommunityPublicId,
            request.HasHubPublicId ? request.HubPublicId : null,
            request.HasSpacePublicId ? request.SpacePublicId : null,
            context.CancellationToken);

        return new CheckGroupAccessResponse
        {
            CanRead = result.CanRead,
            CanWrite = result.CanWrite,
            IsRestricted = result.IsRestricted
        };
    }

    private static CommunityInfo MapToProto(Snakk.Domain.Entities.Community c)
    {
        var info = new CommunityInfo
        {
            PublicId = c.PublicId.Value,
            Name = c.Name,
            Slug = c.Slug,
            ExposeToPlatformFeed = c.ExposeToPlatformFeed,

            Visibility = c.Visibility.ToString(),
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc))
        };

        if (c.Description is not null)
            info.Description = c.Description;

        if (c.Timezone is not null)
            info.Timezone = c.Timezone;

        return info;
    }
}
