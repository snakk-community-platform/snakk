using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Snakk.Shared.Helpers;
using Snakk.Application.UseCases;
using Snakk.Domain.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Protos.User;
using Snakk.Shared.Enums;

namespace Snakk.Api.GrpcServices;

public class UserGrpcService(
    UserProfileUseCase userProfileUseCase,
    IUserRepository userRepository,
    SnakkDbContext dbContext) : UserService.UserServiceBase
{
    public override async Task<UserProfileInfo> GetUserProfile(GetUserProfileRequest request, ServerCallContext context)
    {
        var profile = await userProfileUseCase.GetUserProfileAsync(request.PublicId);

        if (profile is null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        var response = new UserProfileInfo
        {
            PublicId = profile.PublicId,
            DisplayName = profile.DisplayName,
            JoinedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(profile.JoinedAt, DateTimeKind.Utc)),
            DiscussionCount = profile.DiscussionCount,
            PostCount = profile.ReplyCount,
            FollowerCount = profile.FollowerCount,
            FollowingCount = profile.FollowingCount,
            ReplyCount = profile.ReplyCount,
            ReactionsReceived = profile.ReactionsReceived
        };

        if (profile.AvatarFileName is not null)
            response.AvatarFileName = profile.AvatarFileName;

        if (profile.AvatarThumbnailFileName is not null)
            response.AvatarThumbnailFileName = profile.AvatarThumbnailFileName;

        var discord = await dbContext.Users
            .Where(u => u.PublicId == request.PublicId && u.DiscordUserId != null)
            .Select(u => new { u.DiscordUserId, u.DiscordUsername })
            .FirstOrDefaultAsync();
        if (discord is not null)
        {
            response.DiscordUserId = discord.DiscordUserId!;
            response.DiscordUsername = discord.DiscordUsername ?? "";
        }

        if (profile.Bio is not null)
            response.Bio = profile.Bio;

        if (profile.LastSeenAt.HasValue)
            response.LastSeenAt = Timestamp.FromDateTime(
                DateTime.SpecifyKind(profile.LastSeenAt.Value, DateTimeKind.Utc));

        foreach (var a in profile.Achievements)
        {
            var achievement = new UserAchievementInfo
            {
                Slug = a.Slug,
                Name = a.Name,
                Description = a.Description,
                Category = a.Category,
                Tier = a.Tier,
                Points = a.Points,
                EarnedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(a.EarnedAt, DateTimeKind.Utc))
            };

            if (a.IconUrl is not null)
                achievement.IconUrl = a.IconUrl;

            response.Achievements.Add(achievement);
        }

        foreach (var d in profile.TopDiscussions)
        {
            response.TopDiscussions.Add(new TopDiscussionInfo
            {
                PublicId = d.PublicId,
                Title = d.Title,
                Slug = d.Slug,
                PostCount = d.PostCount,
                ReactionCount = d.ReactionCount,
                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(d.CreatedAt, DateTimeKind.Utc)),
                SpaceSlug = d.SpaceSlug,
                SpaceName = d.SpaceName,
                HubSlug = d.HubSlug,
                HubName = d.HubName,
                CommunitySlug = d.CommunitySlug,
                CommunityName = d.CommunityName
            });
        }

        foreach (var s in profile.TopSpaces)
        {
            var space = new TopSpaceInfo
            {
                PublicId = s.SpacePublicId,
                SpaceSlug = s.SpaceSlug,
                SpaceName = s.SpaceName,
                HubSlug = s.HubSlug,
                CommunitySlug = s.CommunitySlug,
                PostCount = s.PostCount
            };

            if (s.SpaceAvatarFileName is not null)
                space.SpaceAvatarFileName = s.SpaceAvatarFileName;

            response.TopSpaces.Add(space);
        }

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
                AvatarUrl = AvatarHelper.GetAvatarUrl(u.PublicId.Value, AvatarEntityType.User, 0, u.AvatarFileName)
            });
        }

        return response;
    }
}
