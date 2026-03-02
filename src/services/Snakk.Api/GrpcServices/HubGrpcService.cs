using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Api.Helpers;
using Snakk.Application.Repositories;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Protos.Hub;
using Snakk.Shared.Enums;

namespace Snakk.Api.GrpcServices;

public class HubGrpcService(
    HubUseCase hubUseCase,
    ISearchRepository searchRepository,
    IHubManagementService hubManagement,
    StatisticsUseCase statisticsUseCase,
    SnakkDbContext dbContext) : HubService.HubServiceBase
{
    public override async Task<HubInfo> GetHubBySlug(GetHubBySlugRequest request, ServerCallContext context)
    {
        var result = await hubUseCase.GetHubBySlugAsync(request.Slug);

        if (!result.IsSuccess || result.Value is null)
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

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value);

        return info;
    }

    public override async Task<PagedHubList> ListHubs(ListHubsRequest request, ServerCallContext context)
    {
        var result = await searchRepository.GetHubsAsync(request.Offset, request.PageSize);

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
        var result = await hubUseCase.GetHubsByCommunityAsync(
            CommunityId.From(request.CommunityId),
            request.Offset,
            request.PageSize);

        var response = new PagedHubList
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var h in result.Items)
        {
            response.Items.Add(MapToProto(h));
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
            SpaceCount = stats.SpaceCount,
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount
        };
    }

    public override async Task<HubRulesResponse> GetHubRules(GetHubRulesRequest request, ServerCallContext context)
    {
        var rules = await hubManagement.GetRulesAsync(request.HubId);

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

    private async Task PopulateRulesMetadata(HubInfo info, string publicId)
    {
        var data = await dbContext.Hubs
            .Where(h => h.PublicId == publicId)
            .Select(h => new {
                h.HasRules,
                h.RulesRevision,
                h.ParentCommunityHasRules })
            .FirstOrDefaultAsync();

        if (data is not null)
        {
            info.HasRules = data.HasRules;
            info.RulesRevision = data.RulesRevision ?? "";
            info.ParentCommunityHasRules = data.ParentCommunityHasRules;
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
