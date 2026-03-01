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
        var userId = RequireAuth();
        var result = await followUseCase.ToggleFollowDiscussionAsync(userId, DiscussionId.From(request.DiscussionId));

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to toggle follow"));

        return new FollowToggleResponse { IsFollowing = result.Value };
    }

    public override async Task<FollowToggleResponse> GetDiscussionFollowStatus(GetDiscussionFollowStatusRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var isFollowing = await followUseCase.IsFollowingDiscussionAsync(userId, DiscussionId.From(request.DiscussionId));
        return new FollowToggleResponse { IsFollowing = isFollowing };
    }

    public override async Task<SpaceFollowToggleResponse> ToggleSpaceFollow(ToggleSpaceFollowRequest request, ServerCallContext context)
    {
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
        var userId = RequireAuth();
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
        var userId = RequireAuth();
        var level = ParseFollowLevel(request.Level);

        var result = await followUseCase.UpdateSpaceFollowLevelAsync(userId, SpaceId.From(request.SpaceId), level);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Failed to update follow level"));

        return new FollowLevelResponse { Level = result.Value.ToString() };
    }

    public override async Task<FollowToggleResponse> ToggleUserFollow(ToggleUserFollowRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var result = await followUseCase.ToggleFollowUserAsync(userId, UserId.From(request.UserId));

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to toggle follow"));

        return new FollowToggleResponse { IsFollowing = result.Value };
    }

    public override async Task<FollowToggleResponse> GetUserFollowStatus(GetUserFollowStatusRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var isFollowing = await followUseCase.IsFollowingUserAsync(userId, UserId.From(request.UserId));
        return new FollowToggleResponse { IsFollowing = isFollowing };
    }

    public override async Task<FollowedIdsResponse> GetFollowedSpaces(GetFollowedSpacesRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var spaceIds = await followUseCase.GetFollowedSpacesAsync(userId);

        var response = new FollowedIdsResponse();
        response.PublicIds.AddRange(spaceIds.Select(id => id.Value));
        return response;
    }

    public override async Task<FollowedIdsResponse> GetFollowedDiscussions(GetFollowedDiscussionsRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var discussionIds = await followUseCase.GetFollowedDiscussionsAsync(userId);

        var response = new FollowedIdsResponse();
        response.PublicIds.AddRange(discussionIds.Select(id => id.Value));
        return response;
    }

    public override async Task<FollowedIdsResponse> GetFollowedUsers(GetFollowedUsersRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var userIds = await followUseCase.GetFollowedUsersAsync(userId);

        var response = new FollowedIdsResponse();
        response.PublicIds.AddRange(userIds.Select(id => id.Value));
        return response;
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

    private static FollowLevel ParseFollowLevel(string? level)
    {
        if (string.IsNullOrEmpty(level) || !Enum.TryParse<FollowLevel>(level, true, out var parsed))
            return FollowLevel.DiscussionsOnly;

        return parsed;
    }
}
