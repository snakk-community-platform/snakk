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
    IEntityHierarchyCacheService hierarchyCache,
    IAllowedTypesService allowedTypesService,
    IDiscussionExtensionService extensionService) : DiscussionService.DiscussionServiceBase
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

        var type = (DiscussionTypeEnum)request.Type;

        // Validate type is allowed in this space
        var effectiveTypes = await allowedTypesService.GetSpaceEffectiveAllowedTypesAsync(request.SpaceId);
        if (!effectiveTypes.Contains(type))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Discussion type '{type}' is not allowed in this space"));

        // Validate type-specific required fields
        ValidateTypeSpecificFields(request, type);

        var slug = request.Title.ToLower().Replace(" ", "-");
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

        // Create type-specific extension records
        await CreateExtensionRecordsAsync(d.PublicId.Value, type, request);

        return new DiscussionCreatedInfo
        {
            PublicId = d.PublicId.Value,
            Title = d.Title,
            Slug = d.Slug,
            CreatedAt = ToTimestamp(d.CreatedAt),
            Type = d.Type.ToString()
        };
    }

    private static void ValidateTypeSpecificFields(CreateDiscussionRequest request, DiscussionTypeEnum type)
    {
        switch (type)
        {
            case DiscussionTypeEnum.Poll:
                if (request.PollOptions.Count < 2)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Poll requires at least 2 options"));
                if (request.PollOptions.Count > 20)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Poll cannot have more than 20 options"));
                break;

            case DiscussionTypeEnum.Debate:
                if (request.DebatePositions.Count < 2)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Debate requires at least 2 positions"));
                if (request.DebatePositions.Count > 3)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Debate cannot have more than 3 positions"));
                break;

            case DiscussionTypeEnum.Link:
                if (!request.HasLinkUrl || string.IsNullOrWhiteSpace(request.LinkUrl))
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Link discussions require a URL"));
                break;
        }
    }

    private async Task CreateExtensionRecordsAsync(string discussionPublicId, DiscussionTypeEnum type, CreateDiscussionRequest request)
    {
        switch (type)
        {
            case DiscussionTypeEnum.Question:
                await extensionService.CreateQuestionAsync(discussionPublicId);
                break;

            case DiscussionTypeEnum.Guide:
                await extensionService.CreateGuideAsync(discussionPublicId);
                break;

            case DiscussionTypeEnum.Poll:
                await extensionService.CreatePollAsync(
                    discussionPublicId,
                    request.PollOptions.ToList(),
                    request.PollAllowMultiple,
                    request.PollAllowChangeVote,
                    request.PollClosesAt is not null ? request.PollClosesAt.ToDateTime() : null);
                break;

            case DiscussionTypeEnum.Link:
                await extensionService.CreateLinkAsync(
                    discussionPublicId,
                    request.LinkUrl);
                break;

            case DiscussionTypeEnum.Debate:
                await extensionService.CreateDebateAsync(
                    discussionPublicId,
                    request.DebatePositions.ToList(),
                    request.DebateAllowNeutral);
                break;

            case DiscussionTypeEnum.Journal:
                await extensionService.CreateJournalAsync(discussionPublicId);
                break;
        }
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
