namespace Snakk.Application.UseCases;

using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;

public record UserProfileDto(
    string PublicId,
    string DisplayName,
    string? AvatarFileName,
    DateTime JoinedAt,
    DateTime? LastSeenAt,
    int DiscussionCount,
    int PostCount,
    int FollowerCount,
    int ReplyCount,
    string? Bio);

public class UserProfileUseCase(
    IUserRepository userRepository) : UseCaseBase
{
    public async Task<UserProfileDto?> GetUserProfileAsync(string publicId)
    {
        var user = await userRepository.GetByPublicIdAsync(UserId.From(publicId));

        if (user is null)
            return null;

        return new UserProfileDto(
            user.PublicId.Value,
            user.DisplayName,
            user.AvatarFileName,
            user.CreatedAt,
            user.LastSeenAt,
            user.DiscussionCount,
            user.ReplyCount,
            user.FollowerCount,
            user.ReplyCount,
            user.Bio);
    }
}
