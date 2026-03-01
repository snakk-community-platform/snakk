using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Shared.Helpers;
using Snakk.Application.UseCases;
using Snakk.Domain.Repositories;
using Snakk.Protos.User;
using Snakk.Shared.Enums;

namespace Snakk.Api.GrpcServices;

public class UserGrpcService(
    UserProfileUseCase userProfileUseCase,
    IUserRepository userRepository) : UserService.UserServiceBase
{
    public override async Task<UserProfileInfo> GetUserProfile(GetUserProfileRequest request, ServerCallContext context)
    {
        var profile = await userProfileUseCase.GetUserProfileAsync(request.PublicId);

        if (profile == null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        var response = new UserProfileInfo
        {
            PublicId = profile.PublicId,
            DisplayName = profile.DisplayName,
            JoinedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(profile.JoinedAt, DateTimeKind.Utc)),
            DiscussionCount = profile.DiscussionCount,
            PostCount = profile.PostCount
        };

        if (profile.AvatarFileName != null)
            response.AvatarFileName = profile.AvatarFileName;

        if (profile.LastSeenAt.HasValue)
            response.LastSeenAt = Timestamp.FromDateTime(
                DateTime.SpecifyKind(profile.LastSeenAt.Value, DateTimeKind.Utc));

        return response;
    }

    public override async Task<UserSearchResults> SearchUsers(SearchUsersRequest request, ServerCallContext context)
    {
        var users = await userRepository.SearchByDisplayNameAsync(request.Query, request.Limit);

        var response = new UserSearchResults();
        foreach (var u in users)
        {
            response.Items.Add(new UserSearchResultItem
            {
                PublicId = u.PublicId.Value,
                DisplayName = u.DisplayName,
                AvatarUrl = AvatarHelper.GetAvatarUrl(u.PublicId.Value, AvatarEntityType.User, 0)
            });
        }

        return response;
    }
}
