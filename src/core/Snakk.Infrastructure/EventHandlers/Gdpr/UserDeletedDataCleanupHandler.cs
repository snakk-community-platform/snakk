namespace Snakk.Infrastructure.EventHandlers.Gdpr;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;

public class UserDeletedDataCleanupHandler(
    SnakkDbContext context,
    ILogger<UserDeletedDataCleanupHandler> logger) : IDomainEventHandler<UserDeletedEvent>
{
    public async Task HandleAsync(UserDeletedEvent @event, CancellationToken ct = default)
    {
        var publicId = @event.UserId.Value;

        // The user row is already soft-deleted — bypass the global query filter to resolve the internal int ID.
        var internalId = await context.Users
            .IgnoreQueryFilters()
            .Where(u => u.PublicId == publicId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (internalId == 0)
        {
            logger.LogWarning("UserDeletedDataCleanupHandler: could not find user {PublicId}", publicId);
            return;
        }

        try
        {
            // --- Credential / session data ---
            await context.RefreshTokens
                .Where(t => t.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            await context.LoginHistory
                .Where(l => l.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            await context.TwoFactorTrustedDevices
                .Where(d => d.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            await context.TwoFactorBackupCodes
                .Where(b => b.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            await context.PasskeyCredentials
                .Where(p => p.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            await context.PasswordResetTokens
                .Where(t => t.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            await context.UserOAuthConnections
                .Where(o => o.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            await context.UserDisplayNameHistories
                .Where(h => h.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            // --- Social graph ---
            await context.UserFollows
                .Where(f => f.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            await context.UserSaves
                .Where(s => s.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            await context.PostReactions
                .Where(r => r.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            // --- Direct messages (hard-delete everything, including soft-deleted rows) ---
            var dmConvIds = await context.DmConversations
                .IgnoreQueryFilters()
                .Where(c => c.InitiatorUserId == internalId || c.RecipientUserId == internalId)
                .Select(c => c.Id)
                .ToListAsync(ct);

            if (dmConvIds.Count > 0)
            {
                await context.DmMessages
                    .IgnoreQueryFilters()
                    .Where(m => dmConvIds.Contains(m.ConversationId))
                    .ExecuteDeleteAsync(ct);

                await context.DmReadStates
                    .Where(r => dmConvIds.Contains(r.ConversationId))
                    .ExecuteDeleteAsync(ct);

                await context.DmConversations
                    .IgnoreQueryFilters()
                    .Where(c => dmConvIds.Contains(c.Id))
                    .ExecuteDeleteAsync(ct);
            }

            // --- Notifications ---
            await context.UserNotifications
                .Where(n => n.RecipientUserId == internalId)
                .ExecuteDeleteAsync(ct);

            // Null out actor reference on notifications triggered by this user so they remain valid
            await context.UserNotifications
                .Where(n => n.ActorUserId == internalId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.ActorUserId, (int?)null)
                    .SetProperty(n => n.ActorUserPublicId, (string?)null), ct);

            // --- Public profile data ---
            await context.UserSocialLinks
                .Where(l => l.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            await context.UserAchievements
                .Where(a => a.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            await context.UserAchievementProgress
                .Where(a => a.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            await context.UserMetrics
                .Where(m => m.UserId == internalId)
                .ExecuteDeleteAsync(ct);

            // --- Keep but strip IP from consent records ---
            await context.UserConsents
                .Where(c => c.UserId == internalId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IpAddress, (string?)null), ct);

            // --- Keep audit logs but wipe free-text details ---
            await context.AuditLogs
                .Where(a => a.ActorUserId == internalId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Details, (string?)null), ct);

            // --- Anonymize denormalized author fields on discussions ---
            await context.Discussions
                .IgnoreQueryFilters()
                .Where(d => d.CreatedByUserId == internalId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.AuthorDisplayName, "Deleted User")
                    .SetProperty(d => d.AuthorAvatarFileName, (string?)null)
                    .SetProperty(d => d.AuthorAvatarThumbnailFileName, (string?)null)
                    .SetProperty(d => d.CreatedByUserPublicId, (string?)null), ct);

            await context.Discussions
                .IgnoreQueryFilters()
                .Where(d => d.LastPostAuthorPublicId == publicId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.LastPostAuthorDisplayName, "Deleted User")
                    .SetProperty(d => d.LastPostAuthorAvatarFileName, (string?)null)
                    .SetProperty(d => d.LastPostAuthorAvatarThumbnailFileName, (string?)null)
                    .SetProperty(d => d.LastPostAuthorPublicId, (string?)null), ct);

            // --- Anonymize post revision edit attribution ---
            await context.PostRevisions
                .Where(r => r.EditedByUserId == internalId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.EditedByUserPublicId, "deleted"), ct);

            logger.LogInformation("Completed GDPR data cleanup for user {PublicId}", publicId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed GDPR data cleanup for user {PublicId}", publicId);
            throw;
        }
    }
}
