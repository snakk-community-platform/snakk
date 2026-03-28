namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

public class UserRepositoryAdapter(
    Infrastructure.Database.Repositories.IUserRepository databaseRepository,
    SnakkDbContext context,
    IEmailProtector emailProtector) : Domain.Repositories.IUserRepository
{
    public async Task<User?> GetByIdAsync(int id)
    {
        var projection = await context.Users
            .Where(u => u.Id == id)
            .Select(u => new UserProjection(u))
            .FirstOrDefaultAsync();
        return projection?.ToDomain(emailProtector);
    }

    public async Task<User?> GetByPublicIdAsync(UserId publicId)
    {
        var projection = await context.Users
            .Where(u => u.PublicId == publicId.Value)
            .Select(u => new UserProjection(u))
            .FirstOrDefaultAsync();
        return projection?.ToDomain(emailProtector);
    }

    public async Task<IEnumerable<User>> GetByPublicIdsAsync(IEnumerable<UserId> publicIds)
    {
        var publicIdStrings = publicIds
            .Select(id => id.Value)
            .ToList();

        if (publicIdStrings.Count == 0) return [];

        var projections = await context.Users
            .Where(u => publicIdStrings.Contains(u.PublicId))
            .Select(u => new UserProjection(u))
            .ToListAsync();

        return projections.Select(p => p.ToDomain(emailProtector));
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var hash = emailProtector.ComputeHash(email);
        var projection = await context.Users
            .Where(u => u.EmailHash == hash)
            .Select(u => new UserProjection(u))
            .FirstOrDefaultAsync();
        return projection?.ToDomain(emailProtector);
    }

    public async Task<User?> GetByOAuthProviderIdAsync(string oauthProviderId)
    {
        var projection = await context.Users
            .Where(u => u.OAuthProviderId == oauthProviderId)
            .Select(u => new UserProjection(u))
            .FirstOrDefaultAsync();
        return projection?.ToDomain(emailProtector);
    }

    public async Task<User?> GetByDisplayNameAsync(string displayName)
    {
        var projection = await context.Users
            .Where(u => u.DisplayName == displayName)
            .Select(u => new UserProjection(u))
            .FirstOrDefaultAsync();
        return projection?.ToDomain(emailProtector);
    }

    public async Task<User?> GetByEmailVerificationTokenAsync(string token)
    {
        var projection = await context.Users
            .Where(u => u.EmailVerificationToken == token)
            .Select(u => new UserProjection(u))
            .FirstOrDefaultAsync();
        return projection?.ToDomain(emailProtector);
    }

    public async Task<IEnumerable<User>> SearchByDisplayNameAsync(string query, int limit)
    {
        var projections = await context.Users
            .Where(u => u.DisplayName.Contains(query))
            .Take(limit)
            .Select(u => new UserProjection(u))
            .ToListAsync();

        return projections.Select(p => p.ToDomain(emailProtector));
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        var projections = await context.Users
            .Select(u => new UserProjection(u))
            .ToListAsync();

        return projections.Select(p => p.ToDomain(emailProtector));
    }

    public async Task AddAsync(User user)
    {
        var plainEmail = user.Email;
        var entity = user.ToPersistence();

        if (plainEmail is not null)
        {
            entity.Email = emailProtector.Protect(plainEmail);
            entity.EmailHash = emailProtector.ComputeHash(plainEmail);
        }

        await databaseRepository.AddAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        var entity = await context.Users.FirstOrDefaultAsync(u => u.PublicId == user.PublicId.Value);

        if (entity is null)
            throw new InvalidOperationException($"User with PublicId '{user.PublicId}' not found");

        var plainEmail = user.Email;

        entity.DisplayName = user.DisplayName;
        entity.Email = plainEmail is not null ? emailProtector.Protect(plainEmail) : null;
        entity.EmailHash = plainEmail is not null ? emailProtector.ComputeHash(plainEmail) : null;
        entity.AvatarFileName = user.AvatarFileName;
        entity.AutoFollowOnReply = user.AutoFollowOnReply;
        entity.Timezone = user.Timezone;
        entity.Bio = user.Bio;
        entity.FeedToken = user.FeedToken;
        entity.LastModifiedAt = user.LastModifiedAt;
        entity.LastSeenAt = user.LastSeenAt;

        await databaseRepository.UpdateAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    private record UserProjection
    {
        public string PublicId { get; init; }
        public string DisplayName { get; init; }
        public string? Email { get; init; }
        public string? PasswordHash { get; init; }
        public bool EmailVerified { get; init; }
        public string? EmailVerificationToken { get; init; }
        public string? OAuthProvider { get; init; }
        public string? OAuthProviderId { get; init; }
        public bool HasGlobalAdminRole { get; init; }
        public string? AvatarFileName { get; init; }
        public int AvatarRevision { get; init; }
        public bool AutoFollowOnReply { get; init; }
        public string? Timezone { get; init; }
        public string? Bio { get; init; }
        public string? FeedToken { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? LastModifiedAt { get; init; }
        public DateTime? LastSeenAt { get; init; }
        public DateTime? LastLoginAt { get; init; }
        public bool NeedsProfileSetup { get; init; }
        public int DiscussionCount { get; init; }
        public int ReplyCount { get; init; }

        public UserProjection(Database.Entities.UserDatabaseEntity u)
        {
            PublicId = u.PublicId;
            DisplayName = u.DisplayName;
            Email = u.Email;
            PasswordHash = u.PasswordHash;
            EmailVerified = u.EmailVerified;
            EmailVerificationToken = u.EmailVerificationToken;
            OAuthProvider = u.OAuthProvider;
            OAuthProviderId = u.OAuthProviderId;
            HasGlobalAdminRole = u.Roles.Any(r =>
                r.RoleId == (int)UserRoleTypeEnum.GlobalAdmin
                && r.RevokedAt is null);
            AvatarFileName = u.AvatarFileName;
            AvatarRevision = u.AvatarRevision;
            AutoFollowOnReply = u.AutoFollowOnReply;
            Timezone = u.Timezone;
            Bio = u.Bio;
            FeedToken = u.FeedToken;
            CreatedAt = u.CreatedAt;
            LastModifiedAt = u.LastModifiedAt;
            LastSeenAt = u.LastSeenAt;
            LastLoginAt = u.LastLoginAt;
            NeedsProfileSetup = u.NeedsProfileSetup;
            DiscussionCount = u.DiscussionCount;
            ReplyCount = u.ReplyCount;
        }

        public User ToDomain(IEmailProtector emailProtector)
        {
            // Decrypt email at the adapter boundary
            string? decryptedEmail = null;
            if (Email is not null)
            {
                try { decryptedEmail = emailProtector.Unprotect(Email); }
                catch { decryptedEmail = Email; } // legacy plaintext email — leave as-is
            }

            return User.Rehydrate(
                UserId.From(PublicId),
                DisplayName, decryptedEmail, PasswordHash,
                EmailVerified, EmailVerificationToken,
                OAuthProvider, OAuthProviderId,
                HasGlobalAdminRole ? "Admin" : null,
                AvatarFileName, AvatarRevision,
                AutoFollowOnReply,
                CreatedAt, LastModifiedAt, LastSeenAt, LastLoginAt,
                NeedsProfileSetup, Timezone, bio: Bio,
                feedToken: FeedToken,
                discussionCount: DiscussionCount,
                replyCount: ReplyCount);
        }
    }
}
