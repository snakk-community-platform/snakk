using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Shared.Helpers;
using Snakk.Api.Services;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Protos;
using Snakk.Protos.Discussion;
using Snakk.Shared.Enums;

namespace Snakk.Api.GrpcServices;

public class DiscussionGrpcService(
    DiscussionUseCase discussionUseCase,
    ISearchRepository searchRepository,
    StatisticsUseCase statisticsUseCase,
    ICurrentUserService currentUser,
    IUserGrantsCacheService grantsCache,
    IEntityHierarchyCacheService hierarchyCache) : DiscussionService.DiscussionServiceBase
{
    public override async Task<DiscussionInfo> GetDiscussion(GetDiscussionRequest request, ServerCallContext context)
    {
        var result = await discussionUseCase.GetDiscussionAsync(DiscussionId.From(request.PublicId));

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        var d = result.Value;

        if (!await IsDiscussionAccessibleAsync(d.PublicId.Value, currentUser.GetCurrentUserId()))
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));
        var postCount = await searchRepository.GetDiscussionPostCountAsync(d.PublicId.Value);
        var info = new DiscussionInfo
        {
            PublicId = d.PublicId.Value,
            Title = d.Title,
            Slug = d.Slug,
            SpaceId = d.SpaceId.Value,
            CreatedAt = ToTimestamp(d.CreatedAt),
            IsPinned = d.IsPinned,
            IsLocked = d.IsLocked,
            Type = d.Type.ToString(),
            PostCount = postCount
        };

        if (d.LastActivityAt.HasValue)
            info.LastActivityAt = ToTimestamp(d.LastActivityAt.Value);

        return info;
    }

    public override async Task<DiscussionCreatedInfo> CreateDiscussion(CreateDiscussionRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();

        if (!await IsSpaceAccessibleAsync(request.SpaceId, userId.Value))
            throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

        var slug = request.Title.ToLower().Replace(" ", "-");
        var type = (Snakk.Shared.Enums.DiscussionTypeEnum)request.Type;
        var result = await discussionUseCase.CreateDiscussionAsync(
            SpaceId.From(request.SpaceId),
            userId,
            request.Title,
            slug,
            request.Content,
            type);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to create discussion"));

        var d = result.Value;

        return new DiscussionCreatedInfo
        {
            PublicId = d.PublicId.Value,
            Title = d.Title,
            Slug = d.Slug,
            CreatedAt = ToTimestamp(d.CreatedAt),
            Type = d.Type.ToString()
        };
    }

    public override async Task<PagedRecentDiscussionList> GetRecentDiscussions(GetRecentDiscussionsRequest request, ServerCallContext context)
    {
        var result = await searchRepository.GetRecentDiscussionsAsync(
            request.Offset,
            request.PageSize,
            request.HasCommunityId ? request.CommunityId : null,
            request.HasHubId ? request.HubId : null,
            null,
            currentUser.GetCurrentUserId());

        var response = new PagedRecentDiscussionList
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var d in result.Items)
        {
            var item = new RecentDiscussionInfo
            {
                PublicId = d.PublicId,
                Title = d.Title,
                Slug = d.Slug,
                Type = ((DiscussionTypeEnum)d.Type).ToString(),
                CreatedAt = ToTimestamp(d.CreatedAt),
                IsPinned = d.IsPinned,
                IsLocked = d.IsLocked,
                PostCount = d.PostCount,
                ReactionCount = d.ReactionCount,

                Space = new EntityRef
                {
                    PublicId = d.SpacePublicId,
                    Slug = d.SpaceSlug,
                    Name = d.SpaceName
                },
                Hub = new EntityRef
                {
                    PublicId = d.HubPublicId,
                    Slug = d.HubSlug,
                    Name = d.HubName
                },
                Community = new EntityRef
                {
                    PublicId = d.CommunityPublicId,
                    Slug = d.CommunitySlug,
                    Name = d.CommunityName
                },
                Author = new AuthorRef
                {
                    PublicId = d.CreatedByUserPublicId,
                    DisplayName = d.CreatedByUserDisplayName,
                    AvatarUrl = AvatarHelper.GetAvatarUrl(d.CreatedByUserPublicId, AvatarEntityType.User, 0)
                }
            };

            if (d.LastActivityAt.HasValue)
                item.LastActivityAt = ToTimestamp(d.LastActivityAt.Value);

            item.Tags.AddRange(d.Tags ?? []);

            response.Items.Add(item);
        }

        return response;
    }

    public override async Task<PagedDiscussionBySpaceList> GetDiscussionsBySpace(GetDiscussionsBySpaceRequest request, ServerCallContext context)
    {
        int? typeFilter = request.HasTypeFilter ? request.TypeFilter : null;

        var result = await searchRepository.GetDiscussionsBySpaceAsync(
            request.SpaceId,
            request.Offset,
            request.PageSize,
            typeFilter,
            currentUser.GetCurrentUserId());

        var response = new PagedDiscussionBySpaceList
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var d in result.Items)
        {
            var item = new DiscussionBySpaceInfo
            {
                PublicId = d.PublicId,
                SpaceId = d.SpacePublicId,
                Title = d.Title,
                Slug = d.Slug,
                Type = ((DiscussionTypeEnum)d.Type).ToString(),
                CreatedAt = ToTimestamp(d.CreatedAt),
                IsPinned = d.IsPinned,
                IsLocked = d.IsLocked,
                PostCount = d.PostCount,
                ReactionCount = d.ReactionCount,

                Author = new AuthorRef
                {
                    PublicId = d.AuthorPublicId,
                    DisplayName = d.AuthorDisplayName,
                    AvatarUrl = AvatarHelper.GetAvatarUrl(d.AuthorPublicId, AvatarEntityType.User, 0)
                }
            };

            if (d.LastActivityAt.HasValue)
                item.LastActivityAt = ToTimestamp(d.LastActivityAt.Value);

            if (d.Tags is not null)
            {
                var tags = d.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                item.Tags.AddRange(tags);
            }

            response.Items.Add(item);
        }

        return response;
    }

    public override async Task<DiscussionPreviewInfo> GetDiscussionPreview(GetDiscussionPreviewRequest request, ServerCallContext context)
    {
        if (!await IsDiscussionAccessibleAsync(request.DiscussionId, currentUser.GetCurrentUserId()))
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        var result = await discussionUseCase.GetFirstPostPreviewAsync(DiscussionId.From(request.DiscussionId));

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        return new DiscussionPreviewInfo { Content = result.Value ?? "" };
    }

    public override async Task<DiscussionStats> GetDiscussionStats(GetDiscussionStatsRequest request, ServerCallContext context)
    {
        if (!await IsDiscussionAccessibleAsync(request.PublicId, currentUser.GetCurrentUserId()))
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        var result = await statisticsUseCase.GetDiscussionStatsAsync(request.PublicId);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        var stats = result.Value;

        return new DiscussionStats
        {
            PublicId = stats.PublicId,
            Title = stats.Title,
            ReplyCount = stats.ReplyCount,
            FollowerCount = stats.FollowerCount
        };
    }

    private async Task<bool> IsDiscussionAccessibleAsync(string discussionPublicId, string? userId)
    {
        var restricted = await grantsCache.GetRestrictedEntitiesAsync();
        if (restricted.IsEmpty) return true;

        var h = await hierarchyCache.GetDiscussionHierarchyAsync(discussionPublicId);
        if (h is null) return false;

        var spaceGate = restricted.SpaceIds.Contains(h.SpaceId);
        var hubGate = restricted.HubIds.Contains(h.HubId);
        var communityGate = restricted.CommunityIds.Contains(h.CommunityId);

        if (!spaceGate && !hubGate && !communityGate) return true;
        if (userId is null) return false;

        var grants = await grantsCache.GetGrantsAsync(userId);
        return (!spaceGate || grants.SpaceIds.Contains(h.SpaceId))
            && (!hubGate || grants.HubIds.Contains(h.HubId))
            && (!communityGate || grants.CommunityIds.Contains(h.CommunityId));
    }

    private async Task<bool> IsSpaceAccessibleAsync(string spacePublicId, string? userId)
    {
        var restricted = await grantsCache.GetRestrictedEntitiesAsync();
        if (restricted.IsEmpty) return true;

        var h = await hierarchyCache.GetSpaceHierarchyAsync(spacePublicId);
        if (h is null) return false;

        var spaceGate = restricted.SpaceIds.Contains(h.Id);
        var hubGate = restricted.HubIds.Contains(h.HubId);
        var communityGate = restricted.CommunityIds.Contains(h.CommunityId);

        if (!spaceGate && !hubGate && !communityGate) return true;
        if (userId is null) return false;

        var grants = await grantsCache.GetGrantsAsync(userId);
        return (!spaceGate || grants.SpaceIds.Contains(h.Id))
            && (!hubGate || grants.HubIds.Contains(h.HubId))
            && (!communityGate || grants.CommunityIds.Contains(h.CommunityId));
    }

    private UserId RequireAuth()
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userId = currentUser.GetCurrentUserId();

        if (userId is null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        return UserId.From(userId);
    }

    private static Timestamp ToTimestamp(DateTime dt) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}
