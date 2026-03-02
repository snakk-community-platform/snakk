using Grpc.Core;
using Snakk.Api.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Protos;
using Snakk.Protos.Reaction;

namespace Snakk.Api.GrpcServices;

public class ReactionGrpcService(
    ReactionUseCase reactionUseCase,
    ICurrentUserService currentUser) : ReactionService.ReactionServiceBase
{
    public override async Task<ToggleReactionResponse> ToggleReaction(ToggleReactionRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();

        if (!Enum.TryParse<ReactionType>(request.ReactionType, true, out var reactionType))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid reaction type"));

        var result = await reactionUseCase.ToggleReactionAsync(
            PostId.From(request.PostId),
            userId,
            reactionType);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to toggle reaction"));

        return new ToggleReactionResponse { Added = result.Value };
    }

    public override async Task<ReactionCounts> GetReactionCounts(GetReactionCountsRequest request, ServerCallContext context)
    {
        var counts = await reactionUseCase.GetReactionCountsAsync(PostId.From(request.PostId));

        return new ReactionCounts
        {
            ThumbsUp = counts.GetValueOrDefault(ReactionType.ThumbsUp, 0),
            Heart = counts.GetValueOrDefault(ReactionType.Heart, 0),
            Eyes = counts.GetValueOrDefault(ReactionType.Eyes, 0),
            Crazy = counts.GetValueOrDefault(ReactionType.Crazy, 0)
        };
    }

    public override async Task<UserReactionResponse> GetMyReaction(GetMyReactionRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var reaction = await reactionUseCase.GetUserReactionAsync(PostId.From(request.PostId), userId);

        var response = new UserReactionResponse();

        if (reaction.HasValue)
            response.Reaction = reaction.Value.ToString();

        return response;
    }

    private UserId RequireAuth()
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userIdStr = currentUser.GetCurrentUserId();

        if (userIdStr is null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        return UserId.From(userIdStr);
    }
}
