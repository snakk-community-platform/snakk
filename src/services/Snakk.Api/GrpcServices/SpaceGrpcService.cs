using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Protos.Space;
using Snakk.Shared.Helpers;

namespace Snakk.Api.GrpcServices;

public class SpaceGrpcService(
    SpaceUseCase spaceUseCase,
    ISearchRepository searchRepository,
    IRuleService ruleService,
    StatisticsUseCase statisticsUseCase,
    SnakkDbContext dbContext) : SpaceService.SpaceServiceBase
{
    public override async Task<SpaceInfo> GetSpaceBySlug(GetSpaceBySlugRequest request, ServerCallContext context)
    {
        var result = await spaceUseCase.GetSpaceBySlugAsync(request.Slug, request.HubSlug);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value);

        return info;
    }

    public override async Task<SpaceInfo> GetSpace(GetSpaceRequest request, ServerCallContext context)
    {
        var result = await spaceUseCase.GetSpaceAsync(SpaceId.From(request.PublicId));

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value);

        return info;
    }

    public override async Task<PagedSpaceByHubList> ListSpacesByHub(ListSpacesByHubRequest request, ServerCallContext context)
    {
        var result = await searchRepository.GetSpacesByHubAsync(
            request.HubId,
            request.Offset,
            request.PageSize);

        var response = new PagedSpaceByHubList
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var s in result.Items)
        {
            var spaceInfo = new SpaceByHubInfo
            {
                PublicId = s.PublicId,
                HubPublicId = s.HubPublicId,
                Name = s.Name,
                Slug = s.Slug,
                Description = s.Description ?? "",
                DiscussionCount = s.DiscussionCount,
                ReplyCount = s.ReplyCount,

                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(s.CreatedAt, DateTimeKind.Utc))
            };

            if (s.LatestDiscussion is not null)
            {
                var ld = s.LatestDiscussion;
                spaceInfo.LatestDiscussion = new LatestDiscussionRef
                {
                    PublicId = ld.PublicId,
                    Title = ld.Title,
                    Slug = ld.Slug,
                    LastActivityAt = Timestamp.FromDateTime(DateTime.SpecifyKind(ld.LastActivityAt, DateTimeKind.Utc)),
                    AuthorPublicId = ld.AuthorPublicId,
                    AuthorDisplayName = ld.AuthorDisplayName,
                    AuthorAvatarFileName = ld.AuthorAvatarFileName ?? "",
                    PostCount = ld.PostCount
                };
            }

            response.Items.Add(spaceInfo);
        }

        return response;
    }

    public override async Task<SpaceRulesResponse> GetSpaceRules(GetSpaceRulesRequest request, ServerCallContext context)
    {
        var rules = await ruleService.GetRulesAsync("Space", request.SpaceId);

        var response = new SpaceRulesResponse();

        foreach (var r in rules.Rules)
        {
            response.Rules.Add(new SpaceRule
            {
                Id = r.Order,
                Title = r.Title,
                Description = r.Description
            });
        }

        return response;
    }

    public override async Task<SpaceStats> GetSpaceStats(GetSpaceStatsRequest request, ServerCallContext context)
    {
        var result = await statisticsUseCase.GetSpaceStatsAsync(request.PublicId);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

        var stats = result.Value;

        return new SpaceStats
        {
            PublicId = stats.PublicId,
            Name = stats.Name,
            Description = stats.Description ?? "",
            AvatarUrl = AvatarHelper.GetAvatarUrl(stats.PublicId, AvatarEntityType.Space, 0),
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount,
            FollowerCount = stats.FollowerCount
        };
    }

    private async Task PopulateRulesMetadata(SpaceInfo info, string publicId)
    {
        var data = await dbContext.Spaces
            .Where(s => s.PublicId == publicId)
            .Select(s => new {
                s.HasRules,
                s.RulesRevision,
                s.ParentHubHasRules,
                s.ParentCommunityHasRules,
                s.TeamRevision,
                AllowedTypes = s.AllowedDiscussionTypes.Select(a => a.DiscussionType).ToList() })
            .FirstOrDefaultAsync();

        if (data is not null)
        {
            info.HasRules = data.HasRules;
            info.RulesRevision = data.RulesRevision ?? "";
            info.ParentHubHasRules = data.ParentHubHasRules;
            info.ParentCommunityHasRules = data.ParentCommunityHasRules;
            info.TeamRevision = data.TeamRevision ?? "";
            info.AllowedDiscussionTypes.AddRange(data.AllowedTypes);
        }
    }

    private static SpaceInfo MapToProto(Snakk.Domain.Entities.Space s)
    {
        var info = new SpaceInfo
        {
            PublicId = s.PublicId.Value,
            HubId = s.HubId.Value,
            Name = s.Name,
            Slug = s.Slug,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(s.CreatedAt, DateTimeKind.Utc))
        };

        if (s.Description is not null)
            info.Description = s.Description;

        return info;
    }
}
