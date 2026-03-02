namespace Snakk.Application.UseCases;

using Snakk.Application.Repositories;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;

public record UserProfileDto(
    string PublicId,
    string DisplayName,
    string? AvatarFileName,
    DateTime JoinedAt,
    DateTime? LastSeenAt,
    int DiscussionCount,
    int PostCount);

public class UserProfileUseCase(
    IUserRepository userRepository,
    ISearchRepository searchRepository) : UseCaseBase
{
    public async Task<UserProfileDto?> GetUserProfileAsync(string publicId)
    {
        var user = await userRepository.GetByPublicIdAsync(UserId.From(publicId));

        if (user is null)
            return null;

        // Get discussion and post counts (sequential to avoid DbContext concurrency issues)
        var discussionCount = await searchRepository.GetDiscussionCountByAuthorAsync(publicId);
        var postCount = await searchRepository.GetPostCountByAuthorAsync(publicId);

        return new UserProfileDto(
            user.PublicId.Value,
            user.DisplayName,
            user.AvatarFileName,
            user.CreatedAt,
            user.LastSeenAt,
            discussionCount,
            postCount);
    }
}
