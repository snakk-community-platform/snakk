namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Snakk.Application.DTOs.Gdpr;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

public class GdprRepository(
    SnakkDbContext context,
    IEmailProtector emailProtector,
    IDataProtectionProvider dataProtectionProvider) : IGdprRepository
{
    private readonly IDataProtector _dmProtector = dataProtectionProvider.CreateProtector("DmMessages");

    private string DecryptDm(string value)
    {
        try { return _dmProtector.Unprotect(value); }
        catch { return value; }
    }

    public async Task<UserDataExportBundle?> ExportUserDataAsync(string publicId, CancellationToken ct = default)
    {
        var user = await context.Users
            .IgnoreQueryFilters()
            .Where(u => u.PublicId == publicId)
            .Select(u => new
            {
                u.Id,
                u.PublicId,
                u.DisplayName,
                u.Email,
                u.Bio,
                u.Timezone,
                u.EmailVerified,
                u.CreatedAt,
                u.LastLoginAt,
                u.LastSeenAt
            })
            .FirstOrDefaultAsync(ct);

        if (user is null) return null;

        var internalId = user.Id;
        var decryptedEmail = user.Email is not null ? emailProtector.Unprotect(user.Email) : null;

        var profile = new ExportProfileDto(
            user.PublicId,
            user.DisplayName,
            decryptedEmail,
            user.Bio,
            user.Timezone,
            user.EmailVerified,
            user.CreatedAt,
            user.LastLoginAt,
            user.LastSeenAt);

        var displayNameHistory = await context.UserDisplayNameHistories
            .Where(h => h.UserId == internalId)
            .OrderBy(h => h.ChangedAt)
            .Select(h => new ExportDisplayNameHistoryDto(h.PreviousName, h.NewName, h.ChangedAt))
            .ToListAsync(ct);

        var socialLinks = await context.UserSocialLinks
            .Where(l => l.UserId == internalId)
            .Select(l => new ExportSocialLinkDto(l.Platform ?? "", l.Username ?? ""))
            .ToListAsync(ct);

        var discussions = await context.Discussions
            .IgnoreQueryFilters()
            .Where(d => d.CreatedByUserId == internalId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new ExportDiscussionDto(d.PublicId, d.Title, d.CreatedAt, d.IsDeleted))
            .ToListAsync(ct);

        var posts = await context.Posts
            .IgnoreQueryFilters()
            .Where(p => p.CreatedByUserId == internalId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ExportPostDto(
                p.PublicId,
                p.DiscussionPublicId ?? "",
                p.Content,
                p.CreatedAt,
                p.IsDeleted))
            .ToListAsync(ct);

        var reactions = await context.PostReactions
            .Where(r => r.UserId == internalId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ExportReactionDto(
                r.PostPublicId ?? "",
                r.TypeId.ToString(),
                r.CreatedAt))
            .ToListAsync(ct);

        var follows = await context.UserFollows
            .Where(f => f.UserId == internalId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new ExportFollowDto(
                f.FollowedUserPublicId,
                f.DiscussionPublicId,
                f.SpacePublicId,
                f.CreatedAt))
            .ToListAsync(ct);

        var saves = await context.UserSaves
            .Where(s => s.UserId == internalId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ExportSaveDto(
                s.Discussion != null ? s.Discussion.PublicId : null,
                s.Post != null ? s.Post.PublicId : null,
                s.CreatedAt))
            .ToListAsync(ct);

        var loginHistory = await context.LoginHistory
            .Where(l => l.UserId == internalId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(500)
            .Select(l => new ExportLoginHistoryDto(
                l.IpAddress,
                l.UserAgent,
                l.DeviceHint,
                l.Success,
                l.CreatedAt))
            .ToListAsync(ct);

        var consentRecords = await context.UserConsents
            .Where(c => c.UserId == internalId)
            .Select(c => new ExportConsentDto(
                c.ConsentTypeVersion.ConsentType.Name,
                c.AcceptedAt))
            .ToListAsync(ct);

        // DMs: load all conversations where user is initiator or recipient, with messages
        var dmConversationData = await context.DmConversations
            .IgnoreQueryFilters()
            .Where(c =>
                c.InitiatorUserId == internalId ||
                c.RecipientUserId == internalId)
            .Select(c => new
            {
                c.PublicId,
                c.InitiatorUserId,
                c.InitiatorUserPublicId,
                InitiatorDisplayName = c.InitiatorUser.DisplayName,
                c.RecipientUserId,
                c.RecipientUserPublicId,
                RecipientDisplayName = c.RecipientUser.DisplayName,
                c.CreatedAt,
                Messages = c.Messages
                    .Where(m => !m.IsDeleted)
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new { m.SenderUserPublicId, m.Content, m.CreatedAt })
                    .ToList()
            })
            .ToListAsync(ct);

        var directMessages = dmConversationData.Select(c =>
        {
            var isInitiator = c.InitiatorUserId == internalId;
            var otherPartyPublicId = isInitiator ? c.RecipientUserPublicId ?? "" : c.InitiatorUserPublicId ?? "";
            var otherPartyDisplayName = isInitiator ? c.RecipientDisplayName : c.InitiatorDisplayName;

            var messages = c.Messages
                .Select(m => new ExportDmMessageDto(
                    m.SenderUserPublicId ?? "",
                    DecryptDm(m.Content),
                    m.CreatedAt))
                .ToList();

            return new ExportDmConversationDto(
                c.PublicId,
                otherPartyPublicId,
                otherPartyDisplayName,
                c.CreatedAt,
                messages);
        }).ToList();

        return new UserDataExportBundle(
            profile,
            displayNameHistory,
            socialLinks,
            discussions,
            posts,
            reactions,
            follows,
            saves,
            loginHistory,
            consentRecords,
            directMessages,
            DateTime.UtcNow);
    }
}
