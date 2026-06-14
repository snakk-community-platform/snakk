using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Api.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Application.Services;
using Snakk.Protos.Community;
using Snakk.Shared.Helpers;
using System.Security.Claims;

namespace Snakk.Api.GrpcServices;

public class CommunityGrpcService(
    CommunityUseCase communityUseCase,
    IRuleService ruleService,
    StatisticsUseCase statisticsUseCase,
    ICommunityDataService communityData,
    IGroupAccessService groupAccessService,
    ICurrentUserService currentUser,
    IUserGrantsCacheService grantsCache,
    IEntityHierarchyCacheService hierarchyCache) : CommunityService.CommunityServiceBase
{
    public override async Task<CommunityInfo> GetCommunityBySlug(GetCommunityBySlugRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var result = await communityUseCase.GetCommunityBySlugAsync(request.Slug);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        if (!await IsCommunityAccessibleAsync(result.Value.PublicId.Value, currentUser.GetCurrentUserId(), ct))
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value, ct);

        return info;
    }

    public override async Task<CommunityInfo> GetCommunityByDomain(GetCommunityByDomainRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var result = await communityUseCase.GetCommunityByDomainAsync(request.Domain);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        if (!await IsCommunityAccessibleAsync(result.Value.PublicId.Value, currentUser.GetCurrentUserId(), ct))
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value, ct);

        return info;
    }

    public override async Task<CommunityInfo> GetCommunity(GetCommunityRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var result = await communityUseCase.GetCommunityAsync(CommunityId.From(request.PublicId));

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        if (!await IsCommunityAccessibleAsync(result.Value.PublicId.Value, currentUser.GetCurrentUserId(), ct))
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value, ct);

        return info;
    }

    public override async Task<PagedCommunityList> ListCommunities(ListCommunitiesRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var result = await communityUseCase.GetPublicCommunitiesAsync(request.Offset, request.PageSize);

        // Fetch denormalized counts via data service (no direct DbContext)
        var publicIds = result.Items.Select(c => c.PublicId.Value).ToList();
        var counts = await communityData.GetCountsByPublicIdsAsync(publicIds, ct);

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
        var ct = context.CancellationToken;
        var result = await statisticsUseCase.GetCommunityStatsAsync(request.PublicId);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        var stats = result.Value;

        return new CommunityStats
        {
            PublicId = stats.PublicId,
            Name = stats.Name,
            Description = stats.Description ?? "",
            AvatarUrl = AvatarHelper.GetAvatarUrl(stats.PublicId, AvatarEntityType.Community, 0, stats.AvatarFileName),
            HubCount = stats.HubCount,
            SpaceCount = stats.SpaceCount,
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount
        };
    }

    public override async Task<CommunityRulesResponse> GetCommunityRules(GetCommunityRulesRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var rules = await ruleService.GetRulesAsync("Community", request.CommunityId, ct);

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
        var ct = context.CancellationToken;
        var rules = await ruleService.GetRulesAsync("Site", null, ct);
        var revision = await ruleService.GetSiteRulesRevisionAsync(ct);

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

    private async Task<bool> IsCommunityAccessibleAsync(string communityPublicId, string? userId, CancellationToken ct = default)
    {
        var restricted = await grantsCache.GetRestrictedEntitiesAsync(ct);
        if (restricted.IsEmpty) return true;

        var communityDbId = await hierarchyCache.GetCommunityIdAsync(communityPublicId, ct);

        if (communityDbId is null) return false;
        if (!restricted.CommunityIds.Contains(communityDbId.Value)) return true;
        if (userId is null) return false;

        var grants = await grantsCache.GetGrantsAsync(userId, ct);
        return grants.CommunityIds.Contains(communityDbId.Value);
    }

    private async Task PopulateRulesMetadata(CommunityInfo info, string publicId, CancellationToken ct = default)
    {
        var data = await communityData.GetCommunityMetaAsync(publicId, ct);

        if (data is not null)
        {
            info.HasRules = data.HasRules;
            info.RulesRevision = data.RulesRevision ?? "";
            info.TeamRevision = data.TeamRevision ?? "";
            info.IsRestricted = data.IsRestricted;
            info.Require2Fa = data.Require2FA;
        }
    }

    public override async Task<CheckGroupAccessResponse> CheckGroupAccess(
        CheckGroupAccessRequest request,
        ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userPublicId = context.GetHttpContext().User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        var result = await groupAccessService.CheckAccessAsync(
            userPublicId,
            request.CommunityPublicId,
            request.HasHubPublicId ? request.HubPublicId : null,
            request.HasSpacePublicId ? request.SpacePublicId : null,
            ct);

        return new CheckGroupAccessResponse
        {
            AccessLevel = (int)result.AccessLevel,
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

        if (c.AvatarFileName is not null)
            info.AvatarFileName = c.AvatarFileName;

        return info;
    }
}
