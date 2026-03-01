using Grpc.Core;
using Snakk.Api.Services;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Protos.ReadState;

namespace Snakk.Api.GrpcServices;

public class ReadStateGrpcService(
    IDiscussionReadStateRepository readStateRepository,
    ICurrentUserService currentUser) : ReadStateService.ReadStateServiceBase
{
    public override async Task<ReadStateInfo> GetReadState(GetReadStateRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();

        var readState = await readStateRepository.GetAsync(
            userId,
            DiscussionId.From(request.DiscussionId));

        var response = new ReadStateInfo();
        if (readState != null)
        {
            if (readState.LastReadPostId != null)
                response.LastReadPostId = readState.LastReadPostId.Value;
            response.LastReadAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.SpecifyKind(readState.LastReadAt, DateTimeKind.Utc));
        }

        return response;
    }

    public override async Task<MarkAsReadResponse> MarkAsRead(MarkAsReadRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();

        var discussionId = DiscussionId.From(request.DiscussionId);
        var postId = PostId.From(request.LastReadPostId);

        var readState = await readStateRepository.GetAsync(userId, discussionId);

        if (readState == null)
        {
            readState = DiscussionReadState.Create(userId, discussionId, postId);
        }
        else
        {
            readState.MarkAsRead(postId);
        }

        await readStateRepository.SaveAsync(readState);

        return new MarkAsReadResponse { Success = true };
    }

    public override async Task<BatchMarkAsReadResponse> BatchMarkAsRead(BatchMarkAsReadRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();

        int processed = 0;
        foreach (var item in request.Items)
        {
            try
            {
                var discussionId = DiscussionId.From(item.DiscussionId);
                var postId = PostId.From(item.LastReadPostId);

                var readState = await readStateRepository.GetAsync(userId, discussionId);

                if (readState == null)
                {
                    readState = DiscussionReadState.Create(userId, discussionId, postId);
                }
                else
                {
                    readState.MarkAsRead(postId);
                }

                await readStateRepository.SaveAsync(readState);
                processed++;
            }
            catch
            {
                continue;
            }
        }

        return new BatchMarkAsReadResponse { Success = true, Processed = processed };
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
}
