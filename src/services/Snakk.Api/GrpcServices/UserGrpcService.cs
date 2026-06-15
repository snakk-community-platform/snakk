using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Shared.Helpers;
using Snakk.Application.UseCases;
using Snakk.Domain.Repositories;
using Snakk.Application.Services;
using Snakk.Protos.User;
using Snakk.Shared.Enums;

namespace Snakk.Api.GrpcServices;

public class UserGrpcService(
    UserProfileUseCase userProfileUseCase,
    IUserRepository userRepository,
    IUserDataService userDataService,
    HybridCache cache) : UserService.UserServiceBase
{
    // Write-path callers (UpdateUserSlugAsync, CreateUserAsync) must call
    // cache.RemoveAsync($"{SlugIdCacheKeyPrefix}{slug}") after committing a slug change.
    internal const string SlugIdCacheKeyPrefix = "user:slug-id:";

    // Write-path callers (UpdateUserSlugAsync when adding a history entry) must call
    // cache.RemoveAsync($"{OldSlugCacheKeyPrefix}{oldSlug}") so the redirect is live immediately.
    internal const string OldSlugCacheKeyPrefix = "user:old-slug:";

    private static readonly HybridCacheEntryOptions SlugIdCacheOptions =
        new() { Expiration = TimeSpan.FromHours(24) };
    private static readonly HybridCacheEntryOptions OldSlugCacheOptions =
        new() { Expiration = TimeSpan.FromHours(1) };

    public override async Task<UserProfileInfo> GetUserProfile(GetUserProfileRequest request, ServerCallContext context)
    {
        var profile = await userProfileUseCase.GetUserProfileAsync(request.PublicId);
        if (profile is null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        return await BuildUserProfileInfoAsync(profile, context.CancellationToken);
    }

    public override async Task<UserProfileInfo> GetUserBySlug(GetUserBySlugRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var cacheKey = $"{SlugIdCacheKeyPrefix}{request.Slug}";

        var publicId = await cache.GetOrCreateAsync(
            cacheKey,
            async ct2 =>
            {
                var slim = await userRepository.GetProfileSlimBySlugAsync(request.Slug, ct2);
                return slim?.PublicId ?? "";
            },
            SlugIdCacheOptions,
            cancellationToken: ct);

        if (string.IsNullOrEmpty(publicId))
        {
            // Not found in DB; evict the empty sentinel immediately so a subsequent
            // registration for this slug isn't blocked by a stale cache entry.
            await cache.RemoveAsync(cacheKey, ct);
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));
        }

        var profile = await userProfileUseCase.GetUserProfileAsync(publicId);
        if (profile is null)
        {
            // Slug mapped to a user that was deleted; evict the stale entry.
            await cache.RemoveAsync(cacheKey, ct);
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));
        }

        return await BuildUserProfileInfoAsync(profile, ct);
    }

    public override async Task<ResolveOldSlugResponse> ResolveOldSlug(ResolveOldSlugRequest request, ServerCallContext context)
    {
        var currentSlug = await cache.GetOrCreateAsync(
            $"{OldSlugCacheKeyPrefix}{request.OldSlug}",
            async ct => (await userRepository.ResolveOldSlugAsync(request.OldSlug, ct)) ?? "",
            OldSlugCacheOptions,
            cancellationToken: context.CancellationToken);

        if (string.IsNullOrEmpty(currentSlug))
            return new ResolveOldSlugResponse { Found = false };
        return new ResolveOldSlugResponse { Found = true, CurrentSlug = currentSlug };
    }

    public override async Task<UserSearchResults> SearchUsers(SearchUsersRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var users = await userRepository.SearchByDisplayNameAsync(request.Query, request.Limit);
        var response = new UserSearchResults();
        foreach (var u in users)
        {
            var item = new UserSearchResultItem
            {
                PublicId = u.PublicId.Value,
                DisplayName = u.DisplayName,
                AvatarUrl = AvatarHelper.GetAvatarUrl(u.PublicId.Value, AvatarEntityType.User, 0, u.AvatarFileName)
            };
            response.Items.Add(item);
        }
        return response;
    }

    private async Task<UserProfileInfo> BuildUserProfileInfoAsync(UserProfileDto profile, CancellationToken ct)
    {
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
            ReactionsReceived = profile.ReactionsReceived,
            IsGlobalAdmin = profile.IsGlobalAdmin
        };

        if (profile.AvatarFileName is not null)
            response.AvatarFileName = profile.AvatarFileName;
        if (profile.AvatarThumbnailFileName is not null)
            response.AvatarThumbnailFileName = profile.AvatarThumbnailFileName;
        if (profile.Slug is not null)
            response.Slug = profile.Slug;
        if (profile.Bio is not null)
            response.Bio = profile.Bio;
        if (profile.LastSeenAt.HasValue)
            response.LastSeenAt = Timestamp.FromDateTime(
                DateTime.SpecifyKind(profile.LastSeenAt.Value, DateTimeKind.Utc));

        var discord = await userDataService.GetDiscordInfoAsync(profile.PublicId, ct);
        if (discord is not null)
        {
            response.DiscordUserId = discord.DiscordUserId;
            response.DiscordUsername = discord.DiscordUsername ?? "";
        }

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
}
