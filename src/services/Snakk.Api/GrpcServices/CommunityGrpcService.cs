using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Protos.Community;

namespace Snakk.Api.GrpcServices;

public class CommunityGrpcService(
    CommunityUseCase communityUseCase,
    ICommunityManagementService communityManagement,
    StatisticsUseCase statisticsUseCase,
    SnakkDbContext dbContext) : CommunityService.CommunityServiceBase
{
    public override async Task<CommunityInfo> GetCommunityBySlug(GetCommunityBySlugRequest request, ServerCallContext context)
    {
        var result = await communityUseCase.GetCommunityBySlugAsync(request.Slug);
        if (!result.IsSuccess || result.Value == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value);
        return info;
    }

    public override async Task<CommunityInfo> GetCommunityByDomain(GetCommunityByDomainRequest request, ServerCallContext context)
    {
        var result = await communityUseCase.GetCommunityByDomainAsync(request.Domain);
        if (!result.IsSuccess || result.Value == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value);
        return info;
    }

    public override async Task<CommunityInfo> GetCommunity(GetCommunityRequest request, ServerCallContext context)
    {
        var result = await communityUseCase.GetCommunityAsync(CommunityId.From(request.PublicId));
        if (!result.IsSuccess || result.Value == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value);
        return info;
    }

    public override async Task<PagedCommunityList> ListCommunities(ListCommunitiesRequest request, ServerCallContext context)
    {
        var result = await communityUseCase.GetPublicCommunitiesAsync(request.Offset, request.PageSize);

        var response = new PagedCommunityList
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var c in result.Items)
        {
            response.Items.Add(MapToProto(c));
        }

        return response;
    }

    public override async Task<CommunityStats> GetCommunityStats(GetCommunityStatsRequest request, ServerCallContext context)
    {
        var result = await statisticsUseCase.GetCommunityStatsAsync(request.PublicId);
        if (!result.IsSuccess || result.Value == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        var stats = result.Value;
        return new CommunityStats
        {
            PublicId = stats.PublicId,
            Name = stats.Name,
            Description = stats.Description ?? "",
            HubCount = stats.HubCount,
            SpaceCount = stats.SpaceCount,
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount
        };
    }

    public override async Task<CommunityRulesResponse> GetCommunityRules(GetCommunityRulesRequest request, ServerCallContext context)
    {
        var rules = await communityManagement.GetRulesAsync(request.CommunityId);

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

    private async Task PopulateRulesMetadata(CommunityInfo info, string publicId)
    {
        var data = await dbContext.Communities
            .Where(c => c.PublicId == publicId)
            .Select(c => new { c.HasRules, c.RulesRevision })
            .FirstOrDefaultAsync();

        if (data != null)
        {
            info.HasRules = data.HasRules;
            info.RulesRevision = data.RulesRevision ?? "";
        }
    }

    private static CommunityInfo MapToProto(Snakk.Domain.Entities.Community c)
    {
        var info = new CommunityInfo
        {
            PublicId = c.PublicId.Value,
            Name = c.Name,
            Slug = c.Slug,
            Visibility = c.Visibility.ToString(),
            ExposeToPlatformFeed = c.ExposeToPlatformFeed,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc))
        };

        if (c.Description != null)
            info.Description = c.Description;

        return info;
    }
}
