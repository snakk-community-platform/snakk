using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Protos.Space;

namespace Snakk.Api.GrpcServices;

public class SpaceGrpcService(
    SpaceUseCase spaceUseCase,
    ISearchRepository searchRepository,
    ISpaceManagementService spaceManagement,
    StatisticsUseCase statisticsUseCase,
    SnakkDbContext dbContext) : SpaceService.SpaceServiceBase
{
    public override async Task<SpaceInfo> GetSpaceBySlug(GetSpaceBySlugRequest request, ServerCallContext context)
    {
        var result = await spaceUseCase.GetSpaceBySlugAsync(request.Slug);
        if (!result.IsSuccess || result.Value == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value);
        return info;
    }

    public override async Task<SpaceInfo> GetSpace(GetSpaceRequest request, ServerCallContext context)
    {
        var result = await spaceUseCase.GetSpaceAsync(SpaceId.From(request.PublicId));
        if (!result.IsSuccess || result.Value == null)
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
                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(s.CreatedAt, DateTimeKind.Utc)),
                DiscussionCount = s.DiscussionCount,
                ReplyCount = s.ReplyCount
            };

            if (s.LatestDiscussion != null)
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
        var rules = await spaceManagement.GetRulesAsync(request.SpaceId);

        var response = new SpaceRulesResponse();
        foreach (var r in rules.Rules)
        {
            response.Rules.Add(new SpaceRule
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description
            });
        }

        return response;
    }

    public override async Task<SpaceStats> GetSpaceStats(GetSpaceStatsRequest request, ServerCallContext context)
    {
        var result = await statisticsUseCase.GetSpaceStatsAsync(request.PublicId);
        if (!result.IsSuccess || result.Value == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

        var stats = result.Value;
        return new SpaceStats
        {
            PublicId = stats.PublicId,
            Name = stats.Name,
            Description = stats.Description ?? "",
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount,
            FollowerCount = stats.FollowerCount
        };
    }

    private async Task PopulateRulesMetadata(SpaceInfo info, string publicId)
    {
        var data = await dbContext.Spaces
            .Where(s => s.PublicId == publicId)
            .Select(s => new { s.HasRules, s.RulesRevision, s.ParentHubHasRules, s.ParentCommunityHasRules })
            .FirstOrDefaultAsync();

        if (data != null)
        {
            info.HasRules = data.HasRules;
            info.RulesRevision = data.RulesRevision ?? "";
            info.ParentHubHasRules = data.ParentHubHasRules;
            info.ParentCommunityHasRules = data.ParentCommunityHasRules;
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

        if (s.Description != null)
            info.Description = s.Description;

        return info;
    }
}
