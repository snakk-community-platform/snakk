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

        var response = new ReactionCounts();

        foreach (var kvp in counts)
            response.Counts[kvp.Key.ToString()] = kvp.Value;

        return response;
    }

    public override async Task<UserReactions> GetMyReactions(GetMyReactionsRequest request, ServerCallContext context)
    {
        var userId = TryGetAuthUserId();

        if (userId is null)
            return new UserReactions();

        var reactions = await reactionUseCase.GetUserReactionsAsync(
            PostId.From(request.PostId), userId);

        var response = new UserReactions();
        response.Reactions.AddRange(reactions.Select(r => r.ToString()));

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
}
