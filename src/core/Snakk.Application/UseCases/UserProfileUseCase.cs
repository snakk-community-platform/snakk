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
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ISearchRepository _searchRepository = searchRepository;

    public async Task<UserProfileDto?> GetUserProfileAsync(string publicId)
    {
        var user = await _userRepository.GetByPublicIdAsync(UserId.From(publicId));
        if (user == null)
            return null;

        // Get discussion and post counts (sequential to avoid DbContext concurrency issues)
        var discussionCount = await _searchRepository.GetDiscussionCountByAuthorAsync(publicId);
        var postCount = await _searchRepository.GetPostCountByAuthorAsync(publicId);

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
