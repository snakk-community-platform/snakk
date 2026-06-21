using Grpc.Core;
using Snakk.Api.Services;
using Snakk.Application.Services;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Protos.ReadState;

namespace Snakk.Api.GrpcServices;

public class ReadStateGrpcService(
    IDiscussionReadStateRepository readStateRepository,
    ICurrentUserService currentUser,
    IUserVisitTracker userVisitTracker) : ReadStateService.ReadStateServiceBase
{
    public override async Task<ReadStateInfo> GetReadState(GetReadStateRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = TryGetAuthUserId();

        if (userId is null)
            return new ReadStateInfo();

        var readState = await readStateRepository.GetAsync(
            userId,
            DiscussionId.From(request.DiscussionId),
            ct);

        var response = new ReadStateInfo();

        if (readState is not null)
        {
            if (readState.LastReadPostId is not null)
                response.LastReadPostId = readState.LastReadPostId.Value;

            response.LastReadAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                DateTime.SpecifyKind(readState.LastReadAt, DateTimeKind.Utc));
        }

        return response;
    }

    public override async Task<MarkAsReadResponse> MarkAsRead(MarkAsReadRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = RequireAuth();

        var discussionId = DiscussionId.From(request.DiscussionId);
        var postId = PostId.From(request.LastReadPostId);

        var readState = await readStateRepository.GetAsync(userId, discussionId, ct);

        if (readState is null)
            readState = DiscussionReadState.Create(userId, discussionId, postId);
        else
            readState.MarkAsRead(postId);

        await readStateRepository.SaveAsync(readState, ct);

        return new MarkAsReadResponse { Success = true };
    }

    public override async Task<BatchMarkAsReadResponse> BatchMarkAsRead(BatchMarkAsReadRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = RequireAuth();

        var states = request.Items
            .Select(item => DiscussionReadState.Create(
                userId,
                DiscussionId.From(item.DiscussionId),
                PostId.From(item.LastReadPostId)))
            .ToList();

        await readStateRepository.BatchSaveAsync(states, ct);

        return new BatchMarkAsReadResponse { Success = true, Processed = states.Count };
    }

    public override async Task<MarkVisitAsReadResponse> MarkVisitAsRead(MarkVisitAsReadRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = RequireAuth();
        await userVisitTracker.ForceNewVisitAsync(userId.Value, ct);
        return new MarkVisitAsReadResponse { Success = true };
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
