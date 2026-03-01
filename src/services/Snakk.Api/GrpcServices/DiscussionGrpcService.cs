using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Shared.Helpers;
using Snakk.Api.Services;
using Snakk.Application.Repositories;
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
    ICurrentUserService currentUser) : DiscussionService.DiscussionServiceBase
{
    public override async Task<DiscussionInfo> GetDiscussion(GetDiscussionRequest request, ServerCallContext context)
    {
        var result = await discussionUseCase.GetDiscussionAsync(DiscussionId.From(request.PublicId));
        if (!result.IsSuccess || result.Value == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        var d = result.Value;
        var info = new DiscussionInfo
        {
            PublicId = d.PublicId.Value,
            Title = d.Title,
            Slug = d.Slug,
            SpaceId = d.SpaceId.Value,
            CreatedAt = ToTimestamp(d.CreatedAt),
            IsPinned = d.IsPinned,
            IsLocked = d.IsLocked
        };

        if (d.LastActivityAt.HasValue)
            info.LastActivityAt = ToTimestamp(d.LastActivityAt.Value);

        return info;
    }

    public override async Task<DiscussionCreatedInfo> CreateDiscussion(CreateDiscussionRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();

        var slug = request.Title.ToLower().Replace(" ", "-");
        var result = await discussionUseCase.CreateDiscussionAsync(
            SpaceId.From(request.SpaceId),
            userId,
            request.Title,
            slug,
            request.Content);

        if (!result.IsSuccess || result.Value == null)
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                result.Error ?? "Failed to create discussion"));

        var d = result.Value;
        return new DiscussionCreatedInfo
        {
            PublicId = d.PublicId.Value,
            Title = d.Title,
            Slug = d.Slug,
            CreatedAt = ToTimestamp(d.CreatedAt)
        };
    }

    public override async Task<PagedRecentDiscussionList> GetRecentDiscussions(GetRecentDiscussionsRequest request, ServerCallContext context)
    {
        var result = await searchRepository.GetRecentDiscussionsAsync(
            request.Offset,
            request.PageSize,
            request.HasCommunityId ? request.CommunityId : null,
            null);

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
        var result = await searchRepository.GetDiscussionsBySpaceAsync(
            request.SpaceId,
            request.Offset,
            request.PageSize);

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

            if (d.Tags != null)
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
        var result = await discussionUseCase.GetFirstPostPreviewAsync(DiscussionId.From(request.DiscussionId));
        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        return new DiscussionPreviewInfo { Content = result.Value ?? "" };
    }

    public override async Task<DiscussionStats> GetDiscussionStats(GetDiscussionStatsRequest request, ServerCallContext context)
    {
        var result = await statisticsUseCase.GetDiscussionStatsAsync(request.PublicId);
        if (!result.IsSuccess || result.Value == null)
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

    private UserId RequireAuth()
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userId = currentUser.GetCurrentUserId();
        if (userId == null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        return UserId.From(userId);
    }

    private static Timestamp ToTimestamp(DateTime dt)
        => Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}
