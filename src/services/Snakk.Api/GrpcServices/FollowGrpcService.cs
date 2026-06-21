using Grpc.Core;
using Snakk.Api.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Protos.Follow;

namespace Snakk.Api.GrpcServices;

public class FollowGrpcService(
    FollowUseCase followUseCase,
    ICurrentUserService currentUser) : FollowService.FollowServiceBase
{
    public override async Task<FollowToggleResponse> ToggleDiscussionFollow(ToggleDiscussionFollowRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = RequireAuth();
        var result = await followUseCase.ToggleFollowDiscussionAsync(userId, DiscussionId.From(request.DiscussionId));

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to toggle follow"));

        return new FollowToggleResponse { IsFollowing = result.Value };
    }

    public override async Task<FollowToggleResponse> GetDiscussionFollowStatus(GetDiscussionFollowStatusRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = TryGetAuthUserId();

        if (userId is null)
            return new FollowToggleResponse { IsFollowing = false };

        var isFollowing = await followUseCase.IsFollowingDiscussionAsync(userId, DiscussionId.From(request.DiscussionId));

        return new FollowToggleResponse { IsFollowing = isFollowing };
    }

    public override async Task<SpaceFollowToggleResponse> ToggleSpaceFollow(ToggleSpaceFollowRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = RequireAuth();
        var level = ParseFollowLevel(request.HasLevel ? request.Level : null);

        var result = await followUseCase.ToggleFollowSpaceAsync(userId, SpaceId.From(request.SpaceId), level);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to toggle follow"));

        return new SpaceFollowToggleResponse
        {
            IsFollowing = result.Value,
            Level = level.ToString()
        };
    }

    public override async Task<SpaceFollowStatusResponse> GetSpaceFollowStatus(GetSpaceFollowStatusRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = TryGetAuthUserId();

        if (userId is null)
            return new SpaceFollowStatusResponse { IsFollowing = false };

        var (isFollowing, level) = await followUseCase.GetSpaceFollowStatusAsync(userId, SpaceId.From(request.SpaceId));

        var response = new SpaceFollowStatusResponse
        {
            IsFollowing = isFollowing
        };

        if (level.HasValue)
            response.Level = level.Value.ToString();

        return response;
    }

    public override async Task<FollowLevelResponse> SetSpaceFollowLevel(SetSpaceFollowLevelRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = RequireAuth();
        var level = ParseFollowLevel(request.Level);

        var result = await followUseCase.UpdateSpaceFollowLevelAsync(userId, SpaceId.From(request.SpaceId), level);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Failed to update follow level"));

        return new FollowLevelResponse { Level = result.Value.ToString() };
    }

    public override async Task<FollowToggleResponse> ToggleUserFollow(ToggleUserFollowRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = RequireAuth();
        var result = await followUseCase.ToggleFollowUserAsync(userId, UserId.From(request.UserId));

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to toggle follow"));

        return new FollowToggleResponse { IsFollowing = result.Value };
    }

    public override async Task<FollowToggleResponse> GetUserFollowStatus(GetUserFollowStatusRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = TryGetAuthUserId();

        if (userId is null)
            return new FollowToggleResponse { IsFollowing = false };

        var isFollowing = await followUseCase.IsFollowingUserAsync(userId, UserId.From(request.UserId));

        return new FollowToggleResponse { IsFollowing = isFollowing };
    }

    public override async Task<FollowedIdsResponse> GetFollowedSpaces(GetFollowedSpacesRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = TryGetAuthUserId();

        if (userId is null)
            return new FollowedIdsResponse();

        var spaceIds = await followUseCase.GetFollowedSpacesAsync(userId);

        var response = new FollowedIdsResponse();
        response.PublicIds.AddRange(spaceIds.Select(id => id.Value));

        return response;
    }

    public override async Task<FollowedIdsResponse> GetFollowedDiscussions(GetFollowedDiscussionsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = TryGetAuthUserId();

        if (userId is null)
            return new FollowedIdsResponse();

        var discussionIds = await followUseCase.GetFollowedDiscussionsAsync(userId);

        var response = new FollowedIdsResponse();
        response.PublicIds.AddRange(discussionIds.Select(id => id.Value));

        return response;
    }

    public override async Task<FollowedDiscussionsDetailedResponse> GetFollowedDiscussionsDetailed(GetFollowedDiscussionsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = TryGetAuthUserId();

        if (userId is null)
            return new FollowedDiscussionsDetailedResponse();

        var items = await followUseCase.GetFollowedDiscussionsWithTimestampsAsync(userId);

        var response = new FollowedDiscussionsDetailedResponse();
        response.Items.AddRange(items.Select(x => new FollowedDiscussionEntry
        {
            PublicId = x.Id.Value,
            FollowedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(x.FollowedAt.ToUniversalTime())
        }));

        return response;
    }

    public override async Task<FollowedIdsResponse> GetFollowedUsers(GetFollowedUsersRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = TryGetAuthUserId();

        if (userId is null)
            return new FollowedIdsResponse();

        var userIds = await followUseCase.GetFollowedUsersAsync(userId);

        var response = new FollowedIdsResponse();
        response.PublicIds.AddRange(userIds.Select(id => id.Value));

        return response;
    }

    private UserId? TryGetAuthUserId()
    {
        if (!currentUser.IsAuthenticated()) return null;
        var id = currentUser.GetCurrentUserId();
        return id is null ? null : UserId.From(id);
    }

    private UserId RequireAuth() =>
        TryGetAuthUserId()
        ?? throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

    private static FollowLevel ParseFollowLevel(string? level)
    {
        if (string.IsNullOrEmpty(level) || !Enum.TryParse<FollowLevel>(level, true, out var parsed))
            return FollowLevel.DiscussionsOnly;

        return parsed;
    }
}
