namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public record UserAvatarSlim(
    string PublicId,
    string? DisplayName,
    string? AvatarFileName,
    string? AvatarThumbnailFileName,
    string? AvatarMicroFileName,
    int AvatarRevision);

public record PostAuthorSlim(
    string PublicId,
    string? DisplayName,
    string? AvatarFileName,
    string? AvatarThumbnailFileName,
    string? AvatarMicroFileName,
    int AvatarRevision,
    DateTime CreatedAt,
    int DiscussionCount,
    int ReplyCount);

public record UserProfileSlim(
    string PublicId,
    string? DisplayName,
    string? AvatarFileName,
    string? AvatarThumbnailFileName,
    DateTime CreatedAt,
    DateTime? LastSeenAt,
    int DiscussionCount,
    int FollowerCount,
    int ReplyCount,
    string? Bio);

public record CurrentUserSlim(
    string PublicId,
    string? DisplayName,
    string? Email,
    bool EmailVerified,
    string? OAuthProvider,
    bool AutoFollowOnReply,
    string? Timezone,
    bool IsDisplayNameLocked,
    bool HasPassword,
    string? AvatarFileName,
    string? Bio,
    string? FeedToken,
    bool? AllowAdultContent,
    int AdultPreviewImageMode,
    DateTime? DisplayNameChangedAt,
    bool HidePresence);

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByPublicIdAsync(UserId publicId);
    Task<IEnumerable<User>> GetByPublicIdsAsync(IEnumerable<UserId> publicIds);
    Task<IEnumerable<UserAvatarSlim>> GetAvatarSlimByPublicIdsAsync(IEnumerable<UserId> publicIds);
    Task<IEnumerable<PostAuthorSlim>> GetPostAuthorSlimByPublicIdsAsync(IEnumerable<UserId> publicIds);
    Task<UserProfileSlim?> GetProfileSlimByPublicIdAsync(UserId publicId);
    Task<CurrentUserSlim?> GetCurrentUserSlimAsync(UserId publicId);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByOAuthProviderIdAsync(string oauthProviderId);
    Task<User?> GetByDisplayNameAsync(string displayName);
    Task<User?> GetByEmailVerificationTokenAsync(string token);
    Task<IEnumerable<User>> SearchByDisplayNameAsync(string query, int limit);
    Task<IEnumerable<User>> GetAllAsync();
    Task AddAsync(User user);
    Task UpdateAsync(User user);
}
