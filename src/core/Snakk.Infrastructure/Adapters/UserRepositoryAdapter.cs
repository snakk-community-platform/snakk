namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

public class UserRepositoryAdapter(
    Infrastructure.Database.Repositories.IUserRepository databaseRepository,
    SnakkDbContext context,
    IEmailProtector emailProtector) : Domain.Repositories.IUserRepository
{
    public async Task<User?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var projection = await context.Users
            .Where(u => u.Id == id)
            .Select(u => new UserProjection(u))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain(emailProtector);
    }

    public async Task<User?> GetByPublicIdAsync(UserId publicId, CancellationToken ct = default)
    {
        var projection = await context.Users
            .Where(u => u.PublicId == publicId.Value)
            .Select(u => new UserProjection(u))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain(emailProtector);
    }

    public async Task<IEnumerable<User>> GetByPublicIdsAsync(IEnumerable<UserId> publicIds, CancellationToken ct = default)
    {
        var publicIdStrings = publicIds
            .Select(id => id.Value)
            .ToList();

        if (publicIdStrings.Count == 0) return [];

        var projections = await context.Users
            .Where(u => publicIdStrings.Contains(u.PublicId))
            .Select(u => new UserProjection(u))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain(emailProtector));
    }

    public async Task<IEnumerable<UserAvatarSlim>> GetAvatarSlimByPublicIdsAsync(IEnumerable<UserId> publicIds, CancellationToken ct = default)
    {
        var ids = publicIds.Select(u => u.Value).ToList();
        if (ids.Count == 0) return [];

        return await context.Users
            .Where(u => ids.Contains(u.PublicId))
            .Select(u => new UserAvatarSlim(
                u.PublicId, u.DisplayName,
                u.AvatarFileName, u.AvatarThumbnailFileName, u.AvatarMicroFileName, u.AvatarRevision))
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<PostAuthorSlim>> GetPostAuthorSlimByPublicIdsAsync(IEnumerable<UserId> publicIds, CancellationToken ct = default)
    {
        var ids = publicIds.Select(u => u.Value).ToList();
        if (ids.Count == 0) return [];

        return await context.Users
            .Where(u => ids.Contains(u.PublicId))
            .Select(u => new PostAuthorSlim(
                u.PublicId, u.DisplayName,
                u.AvatarFileName, u.AvatarThumbnailFileName, u.AvatarMicroFileName, u.AvatarRevision,
                u.CreatedAt, u.DiscussionCount, u.ReplyCount))
            .ToListAsync(ct);
    }

    public async Task<UserProfileSlim?> GetProfileSlimByPublicIdAsync(UserId publicId, CancellationToken ct = default)
    {
        return await context.Users
            .Where(u => u.PublicId == publicId.Value)
            .Select(u => new UserProfileSlim(
                u.PublicId, u.DisplayName,
                u.AvatarFileName, u.AvatarThumbnailFileName,
                u.CreatedAt, u.LastSeenAt,
                u.DiscussionCount, u.FollowerCount, u.ReplyCount, u.Bio))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CurrentUserSlim?> GetCurrentUserSlimAsync(UserId publicId, CancellationToken ct = default)
    {
        return await context.Users
            .Where(u => u.PublicId == publicId.Value)
            .Select(u => new CurrentUserSlim(
                u.PublicId, u.DisplayName,
                u.Email, u.EmailVerified,
                u.OAuthProvider, u.AutoFollowOnReply,
                u.Timezone, u.IsDisplayNameLocked,
                u.PasswordHash != null,
                u.AvatarFileName, u.Bio, u.FeedToken,
                u.AllowAdultContent, u.AdultPreviewImageMode,
                u.DisplayNameChangedAt,
                u.HidePresence))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var hash = emailProtector.ComputeHash(email);
        var projection = await context.Users
            .Where(u => u.EmailHash == hash)
            .Select(u => new UserProjection(u))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain(emailProtector);
    }

    public async Task<User?> GetByOAuthProviderIdAsync(string oauthProviderId, CancellationToken ct = default)
    {
        var projection = await context.Users
            .Where(u => u.OAuthProviderId == oauthProviderId)
            .Select(u => new UserProjection(u))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain(emailProtector);
    }

    public async Task<User?> GetByDisplayNameAsync(string displayName, CancellationToken ct = default)
    {
        var projection = await context.Users
            .Where(u => u.DisplayName == displayName)
            .Select(u => new UserProjection(u))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain(emailProtector);
    }

    public async Task<User?> GetByEmailVerificationTokenAsync(string token, CancellationToken ct = default)
    {
        var projection = await context.Users
            .Where(u => u.EmailVerificationToken == token)
            .Select(u => new UserProjection(u))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain(emailProtector);
    }

    public async Task<IEnumerable<User>> SearchByDisplayNameAsync(string query, int limit, CancellationToken ct = default)
    {
        var projections = await context.Users
            .Where(u => u.DisplayName!.Contains(query))
            .Take(limit)
            .Select(u => new UserProjection(u))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain(emailProtector));
    }

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default)
    {
        var projections = await context.Users
            .Select(u => new UserProjection(u))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain(emailProtector));
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        var plainEmail = user.Email;
        var entity = user.ToPersistence();

        if (plainEmail is not null)
        {
            entity.Email = emailProtector.Protect(plainEmail);
            entity.EmailHash = emailProtector.ComputeHash(plainEmail);
        }

        await databaseRepository.AddAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        var entity = await context.Users.FirstOrDefaultAsync(u => u.PublicId == user.PublicId.Value, ct);

        if (entity is null)
            throw new InvalidOperationException($"User with PublicId '{user.PublicId}' not found");

        var plainEmail = user.Email;

        entity.DisplayName = user.DisplayName;
        if (plainEmail is not null)
        {
            entity.Email = emailProtector.Protect(plainEmail);
            entity.EmailHash = emailProtector.ComputeHash(plainEmail);
        }
        entity.AvatarFileName = user.AvatarFileName;
        entity.AvatarThumbnailFileName = user.AvatarThumbnailFileName;
        entity.AvatarMicroFileName = user.AvatarMicroFileName;
        entity.AvatarRevision = user.AvatarRevision;
        entity.AutoFollowOnReply = user.AutoFollowOnReply;
        entity.AllowAdultContent = user.AllowAdultContent;
        entity.Timezone = user.Timezone;
        entity.Bio = user.Bio;
        entity.FeedToken = user.FeedToken;
        entity.LastModifiedAt = user.LastModifiedAt;
        entity.LastSeenAt = user.LastSeenAt;
        entity.LastLoginAt = user.LastLoginAt;
        entity.EmailVerified = user.EmailVerified;
        entity.EmailVerificationToken = user.EmailVerificationToken;
        entity.EmailVerificationTokenCreatedAt = user.EmailVerificationTokenCreatedAt;
        entity.FailedLoginAttempts = user.FailedLoginAttempts;
        entity.LockoutEnd = user.LockoutEnd;
        entity.AuthVersion = user.AuthVersion;
        entity.AuthVersionUpdatedAt = user.AuthVersionUpdatedAt;

        await databaseRepository.UpdateAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);

        try
        {
            await context.Discussions
                .Where(d => d.CreatedByUserId == entity.Id)
                .ExecuteUpdateAsync(d => d
                    .SetProperty(x => x.AuthorDisplayName, entity.DisplayName)
                    .SetProperty(x => x.AuthorAvatarFileName, entity.AvatarFileName)
                    .SetProperty(x => x.AuthorAvatarThumbnailFileName, entity.AvatarThumbnailFileName), ct);

            await context.Discussions
                .Where(d => d.LastPostAuthorPublicId == entity.PublicId)
                .ExecuteUpdateAsync(d => d
                    .SetProperty(x => x.LastPostAuthorDisplayName, entity.DisplayName)
                    .SetProperty(x => x.LastPostAuthorAvatarFileName, entity.AvatarFileName)
                    .SetProperty(x => x.LastPostAuthorAvatarThumbnailFileName, entity.AvatarThumbnailFileName), ct);
        }
        catch (InvalidOperationException)
        {
            // Bulk update not supported by current DB provider (e.g. in-memory in tests)
        }
    }

    private record UserProjection
    {
        public string PublicId { get; init; }
        public string? DisplayName { get; init; }
        public string? Email { get; init; }
        public string? PasswordHash { get; init; }
        public bool EmailVerified { get; init; }
        public string? EmailVerificationToken { get; init; }
        public DateTime? EmailVerificationTokenCreatedAt { get; init; }
        public string? OAuthProvider { get; init; }
        public string? OAuthProviderId { get; init; }
        public bool HasGlobalAdminRole { get; init; }
        public string? AvatarFileName { get; init; }
        public string? AvatarThumbnailFileName { get; init; }
        public string? AvatarMicroFileName { get; init; }
        public int AvatarRevision { get; init; }
        public bool AutoFollowOnReply { get; init; }
        public bool? AllowAdultContent { get; init; }
        public string? Timezone { get; init; }
        public string? Bio { get; init; }
        public string? FeedToken { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? LastModifiedAt { get; init; }
        public DateTime? LastSeenAt { get; init; }
        public DateTime? LastLoginAt { get; init; }
        public int FailedLoginAttempts { get; init; }
        public DateTime? LockoutEnd { get; init; }
        public bool NeedsProfileSetup { get; init; }
        public int DiscussionCount { get; init; }
        public int ReplyCount { get; init; }
        public long AuthVersion { get; init; }
        public DateTime AuthVersionUpdatedAt { get; init; }

        public UserProjection(Database.Entities.UserDatabaseEntity u)
        {
            PublicId = u.PublicId;
            DisplayName = u.DisplayName;
            Email = u.Email;
            PasswordHash = u.PasswordHash;
            EmailVerified = u.EmailVerified;
            EmailVerificationToken = u.EmailVerificationToken;
            EmailVerificationTokenCreatedAt = u.EmailVerificationTokenCreatedAt;
            OAuthProvider = u.OAuthProvider;
            OAuthProviderId = u.OAuthProviderId;
            HasGlobalAdminRole = u.Roles.Any(r =>
                r.RoleId == (int)UserRoleTypeEnum.GlobalAdmin
                && r.RevokedAt is null);
            AvatarFileName = u.AvatarFileName;
            AvatarThumbnailFileName = u.AvatarThumbnailFileName;
            AvatarMicroFileName = u.AvatarMicroFileName;
            AvatarRevision = u.AvatarRevision;
            AutoFollowOnReply = u.AutoFollowOnReply;
            AllowAdultContent = u.AllowAdultContent;
            Timezone = u.Timezone;
            Bio = u.Bio;
            FeedToken = u.FeedToken;
            CreatedAt = u.CreatedAt;
            LastModifiedAt = u.LastModifiedAt;
            LastSeenAt = u.LastSeenAt;
            LastLoginAt = u.LastLoginAt;
            FailedLoginAttempts = u.FailedLoginAttempts;
            LockoutEnd = u.LockoutEnd;
            NeedsProfileSetup = u.NeedsProfileSetup;
            DiscussionCount = u.DiscussionCount;
            ReplyCount = u.ReplyCount;
            AuthVersion = u.AuthVersion;
            AuthVersionUpdatedAt = u.AuthVersionUpdatedAt;
        }

        public User ToDomain(IEmailProtector emailProtector)
        {
            var decryptedEmail = Email is not null ? emailProtector.Unprotect(Email) : null;

            return User.Rehydrate(
                UserId.From(PublicId),
                DisplayName, decryptedEmail, PasswordHash,
                EmailVerified, EmailVerificationToken,
                OAuthProvider, OAuthProviderId,
                HasGlobalAdminRole ? "Admin" : null,
                AvatarFileName, AvatarThumbnailFileName, AvatarMicroFileName, AvatarRevision,
                AutoFollowOnReply,
                CreatedAt, LastModifiedAt, LastSeenAt, LastLoginAt,
                NeedsProfileSetup, Timezone, bio: Bio,
                feedToken: FeedToken,
                allowAdultContent: AllowAdultContent,
                discussionCount: DiscussionCount,
                replyCount: ReplyCount,
                failedLoginAttempts: FailedLoginAttempts,
                lockoutEnd: LockoutEnd,
                emailVerificationTokenCreatedAt: EmailVerificationTokenCreatedAt,
                authVersion: AuthVersion,
                authVersionUpdatedAt: AuthVersionUpdatedAt);
        }
    }
}
