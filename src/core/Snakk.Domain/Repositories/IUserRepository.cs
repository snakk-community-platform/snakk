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
    Task<User?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<User?> GetByPublicIdAsync(UserId publicId, CancellationToken ct = default);
    Task<IEnumerable<User>> GetByPublicIdsAsync(IEnumerable<UserId> publicIds, CancellationToken ct = default);
    Task<IEnumerable<UserAvatarSlim>> GetAvatarSlimByPublicIdsAsync(IEnumerable<UserId> publicIds, CancellationToken ct = default);
    Task<IEnumerable<PostAuthorSlim>> GetPostAuthorSlimByPublicIdsAsync(IEnumerable<UserId> publicIds, CancellationToken ct = default);
    Task<UserProfileSlim?> GetProfileSlimByPublicIdAsync(UserId publicId, CancellationToken ct = default);
    Task<CurrentUserSlim?> GetCurrentUserSlimAsync(UserId publicId, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByOAuthProviderIdAsync(string oauthProviderId, CancellationToken ct = default);
    Task<User?> GetByDisplayNameAsync(string displayName, CancellationToken ct = default);
    Task<User?> GetByEmailVerificationTokenAsync(string token, CancellationToken ct = default);
    Task<IEnumerable<User>> SearchByDisplayNameAsync(string query, int limit, CancellationToken ct = default);
    Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
}
