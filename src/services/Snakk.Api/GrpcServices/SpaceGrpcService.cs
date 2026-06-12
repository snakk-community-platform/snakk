using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Api.Services;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Infrastructure.Database;
using Snakk.Protos.Space;
using Snakk.Shared.Helpers;

namespace Snakk.Api.GrpcServices;

public class SpaceGrpcService(
    SpaceUseCase spaceUseCase,
    ISearchRepository searchRepository,
    IRuleService ruleService,
    StatisticsUseCase statisticsUseCase,
    SnakkDbContext dbContext,
    ICurrentUserService currentUser,
    IUserGrantsCacheService grantsCache,
    IEntityHierarchyCacheService hierarchyCache,
    HybridCache cache) : SpaceService.SpaceServiceBase
{
    public override async Task<SpaceInfo> GetSpaceBySlug(GetSpaceBySlugRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var result = await spaceUseCase.GetSpaceBySlugAsync(request.Slug, request.HubSlug);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

        if (!await IsSpaceAccessibleAsync(result.Value.PublicId.Value, currentUser.GetCurrentUserId(), ct))
            throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value);
        await PopulateDiscordInviteUrlAsync(info, result.Value.PublicId.Value);

        return info;
    }

    public override async Task<SpaceInfo> GetSpace(GetSpaceRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var result = await spaceUseCase.GetSpaceAsync(SpaceId.From(request.PublicId));

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

        if (!await IsSpaceAccessibleAsync(result.Value.PublicId.Value, currentUser.GetCurrentUserId(), ct))
            throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

        var info = MapToProto(result.Value);
        await PopulateRulesMetadata(info, result.Value.PublicId.Value);
        await PopulateDiscordInviteUrlAsync(info, result.Value.PublicId.Value);

        return info;
    }

    public override async Task<PagedSpaceByHubList> ListSpacesByHub(ListSpacesByHubRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = currentUser.GetCurrentUserId();
        var result = await searchRepository.GetSpacesByHubAsync(
            request.HubId,
            request.Offset,
            request.PageSize,
            userId,
            ct);

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

            if (s.AvatarFileName is not null)
                spaceInfo.AvatarFileName = s.AvatarFileName;

            if (s.LatestDiscussion is not null)
            {
                var ld = s.LatestDiscussion;
                spaceInfo.LatestDiscussion = new LatestDiscussionRef
                {
                    PublicId = ld.PublicId,
                    Title = ld.Title,
                    Slug = ld.Slug,
                    LastActivityAt = Timestamp.FromDateTime(DateTime.SpecifyKind(ld.LastActivityAt, DateTimeKind.Utc)),
                    // Coalesce: GDPR-anonymized authors have null ids; proto setters throw on null
                    AuthorPublicId = ld.AuthorPublicId ?? "",
                    AuthorDisplayName = ld.AuthorDisplayName ?? "",
                    AuthorAvatarFileName = ld.AuthorAvatarFileName ?? "",
                    PostCount = ld.PostCount
                };
            }

            response.Items.Add(spaceInfo);
        }

        return response;
    }

    public override async Task<SearchSpacesResponse> SearchSpaces(SearchSpacesRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = currentUser.GetCurrentUserId();
        var items = await searchRepository.SearchSpacesAsync(
            request.HasQuery ? request.Query : null,
            request.HasHubId ? request.HubId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit > 0 ? request.Limit : 10,
            userId,
            ct);

        var response = new SearchSpacesResponse();

        foreach (var s in items)
        {
            response.Items.Add(new SpaceSearchItem
            {
                PublicId = s.PublicId,
                Name = s.Name,
                Slug = s.Slug,
                HubSlug = s.HubSlug,
                HubName = s.HubName,
                CommunitySlug = s.CommunitySlug,
                DiscussionCount = s.DiscussionCount,
                CommunityName = s.CommunityName,
                AvatarUrl = s.AvatarUrl
            });
        }

        return response;
    }

    public override async Task<SpaceRulesResponse> GetSpaceRules(GetSpaceRulesRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var rules = await ruleService.GetRulesAsync("Space", request.SpaceId, ct);

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
        var ct = context.CancellationToken;
        var result = await statisticsUseCase.GetSpaceStatsAsync(request.PublicId);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

        var stats = result.Value;

        return new SpaceStats
        {
            PublicId = stats.PublicId,
            Name = stats.Name,
            Description = stats.Description ?? "",
            AvatarUrl = AvatarHelper.GetAvatarUrl(stats.PublicId, AvatarEntityType.Space, 0, stats.AvatarFileName),
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount,
            FollowerCount = stats.FollowerCount
        };
    }

    private async Task<bool> IsSpaceAccessibleAsync(string spacePublicId, string? userId, CancellationToken ct = default)
    {
        var restricted = await grantsCache.GetRestrictedEntitiesAsync(ct);
        if (restricted.IsEmpty) return true;

        var h = await hierarchyCache.GetSpaceHierarchyAsync(spacePublicId, ct);

        if (h is null) return false;

        var spaceGate = restricted.SpaceIds.Contains(h.Id);
        var hubGate = restricted.HubIds.Contains(h.HubId);
        var communityGate = restricted.CommunityIds.Contains(h.CommunityId);

        if (!spaceGate && !hubGate && !communityGate) return true;
        if (userId is null) return false;

        var grants = await grantsCache.GetGrantsAsync(userId, ct);
        return (!spaceGate || grants.SpaceIds.Contains(h.Id))
            && (!hubGate || grants.HubIds.Contains(h.HubId))
            && (!communityGate || grants.CommunityIds.Contains(h.CommunityId));
    }

    private sealed record LatestDiscussionMeta(string PublicId, string Title, string Slug, DateTime LastActivityAt, string AuthorPublicId, string AuthorDisplayName, string? AuthorAvatarFileName, int PostCount);
    private sealed record SpaceMeta(bool HasRules, string? RulesRevision, bool ParentHubHasRules, bool ParentCommunityHasRules, string? TeamRevision, bool IsRestricted, List<int> AllowedTypes, string? HubSlug, string? CommunitySlug, int DiscussionCount = 0, int ReplyCount = 0, LatestDiscussionMeta? LatestDiscussion = null, bool Require2FA = false);
    private async Task PopulateDiscordInviteUrlAsync(SpaceInfo info, string publicId)
    {
        var inviteUrl = await dbContext.Spaces
            .Where(s => s.PublicId == publicId && s.DiscordInviteUrl != null)
            .Select(s => s.DiscordInviteUrl)
            .FirstOrDefaultAsync();
        if (inviteUrl is not null)
            info.DiscordInviteUrl = inviteUrl;
    }

    private static readonly HybridCacheEntryOptions MetaCacheOptions = new() { Expiration = TimeSpan.FromMinutes(5) };

    private async Task PopulateRulesMetadata(SpaceInfo info, string publicId)
    {
        var data = await cache.GetOrCreateAsync<SpaceMeta?>(
            $"space-meta:{publicId}",
            async cancel =>
            {
                var raw = await dbContext.Spaces
                    .Where(s => s.PublicId == publicId)
                    .Select(s => new {
                        s.Id,
                        s.HasRules,
                        s.RulesRevision,
                        s.ParentHubHasRules,
                        s.ParentCommunityHasRules,
                        s.TeamRevision,
                        s.IsRestricted,
                        s.Require2FA,
                        AllowedTypes = s.AllowedDiscussionTypes.Select(a => a.DiscussionType).ToList(),
                        HubSlug = s.Hub.Slug,
                        CommunitySlug = s.Hub.Community.Slug,
                        s.DiscussionCount,
                        ReplyCount = s.PostCount - s.DiscussionCount,
                    })
                    .FirstOrDefaultAsync(cancel);
                if (raw is null) return null;
                // Separate query avoids the ROW_NUMBER() window-function scan EF Core generates
                // when FirstOrDefault() is used on a navigation collection inside a projection.
                var latestRaw = await dbContext.Discussions
                    .Where(d => d.SpaceId == raw.Id && !d.IsDeleted)
                    .OrderByDescending(d => d.LastActivityAt ?? d.CreatedAt)
                    .Select(d => new {
                        d.PublicId,
                        d.Title,
                        d.Slug,
                        LastActivityAt = d.LastActivityAt ?? d.CreatedAt,
                        AuthorPublicId = d.CreatedByUser.PublicId,
                        AuthorDisplayName = d.CreatedByUser.DisplayName,
                        AuthorAvatarFileName = d.CreatedByUser.AvatarFileName,
                        d.PostCount })
                    .FirstOrDefaultAsync(cancel);
                var ld = latestRaw is null ? null : new LatestDiscussionMeta(
                    latestRaw.PublicId, latestRaw.Title, latestRaw.Slug,
                    latestRaw.LastActivityAt, latestRaw.AuthorPublicId,
                    latestRaw.AuthorDisplayName ?? "", latestRaw.AuthorAvatarFileName,
                    latestRaw.PostCount);
                return new SpaceMeta(raw.HasRules, raw.RulesRevision, raw.ParentHubHasRules, raw.ParentCommunityHasRules, raw.TeamRevision, raw.IsRestricted, raw.AllowedTypes, raw.HubSlug, raw.CommunitySlug, raw.DiscussionCount, raw.ReplyCount, ld, raw.Require2FA);
            },
            MetaCacheOptions);

        if (data is not null)
        {
            info.HasRules = data.HasRules;
            info.RulesRevision = data.RulesRevision ?? "";
            info.ParentHubHasRules = data.ParentHubHasRules;
            info.ParentCommunityHasRules = data.ParentCommunityHasRules;
            info.TeamRevision = data.TeamRevision ?? "";
            info.IsRestricted = data.IsRestricted;
            info.AllowedDiscussionTypes.AddRange(data.AllowedTypes);
            info.HubSlug = data.HubSlug ?? "";
            info.CommunitySlug = data.CommunitySlug ?? "";
            info.DiscussionCount = data.DiscussionCount;
            info.ReplyCount = data.ReplyCount;
            info.Require2Fa = data.Require2FA;
            if (data.LatestDiscussion is not null)
            {
                var ld = data.LatestDiscussion;
                info.LatestDiscussion = new LatestDiscussionRef
                {
                    PublicId = ld.PublicId,
                    Title = ld.Title,
                    Slug = ld.Slug,
                    LastActivityAt = Timestamp.FromDateTime(DateTime.SpecifyKind(ld.LastActivityAt, DateTimeKind.Utc)),
                    // Coalesce: GDPR-anonymized authors have null ids; proto setters throw on null
                    AuthorPublicId = ld.AuthorPublicId ?? "",
                    AuthorDisplayName = ld.AuthorDisplayName ?? "",
                    PostCount = ld.PostCount
                };
                if (ld.AuthorAvatarFileName is not null)
                    info.LatestDiscussion.AuthorAvatarFileName = ld.AuthorAvatarFileName;
            }
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
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(s.CreatedAt, DateTimeKind.Utc)),
            IsAdultOnly = s.IsAdultOnly,
            AllowsAdultContent = s.AllowsAdultContent
        };

        if (s.Description is not null)
            info.Description = s.Description;

        if (s.AvatarFileName is not null)
            info.AvatarFileName = s.AvatarFileName;

        if (s.AvatarThumbnailFileName is not null)
            info.AvatarThumbnailFileName = s.AvatarThumbnailFileName;

        return info;
    }
}
