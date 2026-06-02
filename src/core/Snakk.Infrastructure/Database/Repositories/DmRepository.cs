namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Application.UseCases;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Helpers;
using Snakk.Shared.Models;

public class DmRepository(SnakkDbContext context, IDataProtectionProvider dataProtectionProvider) : IDmRepository
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("DmMessages");

    private string Encrypt(string value) => _protector.Protect(value);

    private string Decrypt(string value)
    {
        try { return _protector.Unprotect(value); }
        catch { return value; }
    }

    public async Task<PagedResult<DmConversationDto>> GetConversationsAsync(
        string callerPublicId, int offset, int pageSize, CancellationToken ct = default)
    {
        var items = await context.DmConversations
            .Where(c =>
                (c.InitiatorUserPublicId == callerPublicId && !c.HiddenFromInitiator) ||
                (c.RecipientUserPublicId == callerPublicId && !c.HiddenFromRecipient))
            .OrderByDescending(c =>
                (c.InitiatorUserPublicId == callerPublicId && c.InitiatorPinned) ||
                (c.RecipientUserPublicId == callerPublicId && c.RecipientPinned))
            .ThenByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(c => new
            {
                c.PublicId,
                c.LastMessageAt,
                c.LastMessageExcerpt,
                c.CreatedAt,
                InitiatorPublicId = c.InitiatorUserPublicId,
                InitiatorDisplayName = c.InitiatorUser.DisplayName,
                InitiatorAvatarFileName = c.InitiatorUser.AvatarThumbnailFileName,
                RecipientPublicId = c.RecipientUserPublicId,
                RecipientDisplayName = c.RecipientUser.DisplayName,
                RecipientAvatarFileName = c.RecipientUser.AvatarThumbnailFileName,
                InitiatorPinned = c.InitiatorPinned,
                RecipientPinned = c.RecipientPinned,
            })
            .ToListAsync(ct);

        var hasMore = items.Count > pageSize;

        var dtos = items.Take(pageSize).Select(c =>
        {
            var isInitiator = c.InitiatorPublicId == callerPublicId;
            var otherPublicId = isInitiator ? c.RecipientPublicId ?? "" : c.InitiatorPublicId ?? "";
            var otherDisplayName = isInitiator ? c.RecipientDisplayName ?? "" : c.InitiatorDisplayName ?? "";
            var otherAvatarFileName = isInitiator ? c.RecipientAvatarFileName : c.InitiatorAvatarFileName;
            var isPinned = isInitiator ? c.InitiatorPinned : c.RecipientPinned;
            var excerpt = c.LastMessageExcerpt != null ? Decrypt(c.LastMessageExcerpt) : null;

            return new DmConversationDto(
                c.PublicId,
                new DmUserDto(otherPublicId, otherDisplayName, otherAvatarFileName),
                excerpt,
                c.LastMessageAt ?? c.CreatedAt,
                isPinned);
        }).ToList();

        return new PagedResult<DmConversationDto>
        {
            Items = dtos,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMore
        };
    }

    public async Task<DmConversationDto?> GetConversationAsync(
        string conversationPublicId, string callerPublicId, CancellationToken ct = default)
    {
        var c = await context.DmConversations
            .Where(c => c.PublicId == conversationPublicId &&
                        ((c.InitiatorUserPublicId == callerPublicId && !c.HiddenFromInitiator) ||
                         (c.RecipientUserPublicId == callerPublicId && !c.HiddenFromRecipient)))
            .Select(c => new
            {
                c.PublicId,
                c.LastMessageAt,
                c.LastMessageExcerpt,
                c.CreatedAt,
                InitiatorPublicId = c.InitiatorUserPublicId,
                InitiatorDisplayName = c.InitiatorUser.DisplayName,
                InitiatorAvatarFileName = c.InitiatorUser.AvatarThumbnailFileName,
                RecipientPublicId = c.RecipientUserPublicId,
                RecipientDisplayName = c.RecipientUser.DisplayName,
                RecipientAvatarFileName = c.RecipientUser.AvatarThumbnailFileName,
                InitiatorPinned = c.InitiatorPinned,
                RecipientPinned = c.RecipientPinned,
            })
            .FirstOrDefaultAsync(ct);

        if (c is null) return null;

        var isInitiator = c.InitiatorPublicId == callerPublicId;
        var otherPublicId = isInitiator ? c.RecipientPublicId ?? "" : c.InitiatorPublicId ?? "";
        var otherDisplayName = isInitiator ? c.RecipientDisplayName ?? "" : c.InitiatorDisplayName ?? "";
        var otherAvatarFileName = isInitiator ? c.RecipientAvatarFileName : c.InitiatorAvatarFileName;
        var isPinned = isInitiator ? c.InitiatorPinned : c.RecipientPinned;
        var excerpt = c.LastMessageExcerpt != null ? Decrypt(c.LastMessageExcerpt) : null;

        return new DmConversationDto(
            c.PublicId,
            new DmUserDto(otherPublicId, otherDisplayName, otherAvatarFileName),
            excerpt,
            c.LastMessageAt ?? c.CreatedAt,
            isPinned);
    }

    public async Task<bool> IsParticipantAsync(
        string conversationPublicId, string callerPublicId, CancellationToken ct = default)
        => await context.DmConversations.AnyAsync(
            c => c.PublicId == conversationPublicId &&
                 (c.InitiatorUserPublicId == callerPublicId || c.RecipientUserPublicId == callerPublicId),
            ct);

    public async Task<PagedResult<DmMessageDto>> GetMessagesAsync(
        string conversationPublicId, string callerPublicId,
        int offset, int pageSize, CancellationToken ct = default)
    {
        var convId = await context.DmConversations
            .Where(c => c.PublicId == conversationPublicId &&
                        (c.InitiatorUserPublicId == callerPublicId || c.RecipientUserPublicId == callerPublicId))
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);

        if (convId is null) return new PagedResult<DmMessageDto>();

        var raw = await context.DmMessages
            .Where(m => m.ConversationId == convId.Value &&
                        !(m.SenderUserPublicId == callerPublicId && m.HiddenFromSender) &&
                        !(m.SenderUserPublicId != callerPublicId && m.HiddenFromRecipient))
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(m => new
            {
                m.PublicId,
                SenderPublicId = m.SenderUserPublicId ?? "",
                m.Content,
                m.CreatedAt,
                IsMine = m.SenderUserPublicId == callerPublicId,
            })
            .ToListAsync(ct);

        var hasMore = raw.Count > pageSize;

        var messages = raw.Take(pageSize).Select(m => new DmMessageDto(
            m.PublicId,
            m.SenderPublicId,
            Decrypt(m.Content),
            m.CreatedAt,
            m.IsMine)).ToList();

        return new PagedResult<DmMessageDto>
        {
            Items = messages,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMore
        };
    }

    public async Task<DmConversationDto?> GetOrCreateConversationAsync(
        string initiatorPublicId, string recipientPublicId, CancellationToken ct = default)
    {
        var existing = await context.DmConversations
            .Where(c =>
                (c.InitiatorUserPublicId == initiatorPublicId && c.RecipientUserPublicId == recipientPublicId) ||
                (c.InitiatorUserPublicId == recipientPublicId && c.RecipientUserPublicId == initiatorPublicId))
            .Select(c => c.PublicId)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
            return await GetConversationAsync(existing, initiatorPublicId, ct);

        var users = await context.Users
            .Where(u => u.PublicId == initiatorPublicId || u.PublicId == recipientPublicId)
            .Select(u => new { u.Id, u.PublicId })
            .ToListAsync(ct);

        var initiator = users.FirstOrDefault(u => u.PublicId == initiatorPublicId);
        var recipient = users.FirstOrDefault(u => u.PublicId == recipientPublicId);

        if (initiator is null || recipient is null) return null;

        var publicId = UlidBase62.Encode(Ulid.NewUlid().ToString());
        var conversation = new DmConversationDatabaseEntity
        {
            PublicId = publicId,
            InitiatorUserId = initiator.Id,
            InitiatorUserPublicId = initiator.PublicId,
            RecipientUserId = recipient.Id,
            RecipientUserPublicId = recipient.PublicId,
            CreatedAt = DateTime.UtcNow
        };

        context.DmConversations.Add(conversation);
        await context.SaveChangesAsync(ct);

        return await GetConversationAsync(publicId, initiatorPublicId, ct);
    }

    public async Task<DmMessageDto?> SaveMessageAsync(
        string conversationPublicId, string senderPublicId,
        string content, string excerpt, CancellationToken ct = default)
    {
        var conversation = await context.DmConversations
            .Where(c => c.PublicId == conversationPublicId &&
                        (c.InitiatorUserPublicId == senderPublicId || c.RecipientUserPublicId == senderPublicId))
            .FirstOrDefaultAsync(ct);

        if (conversation is null) return null;

        var sender = await context.Users
            .Where(u => u.PublicId == senderPublicId)
            .Select(u => new { u.Id, u.PublicId })
            .FirstOrDefaultAsync(ct);

        if (sender is null) return null;

        var messagePublicId = UlidBase62.Encode(Ulid.NewUlid().ToString());
        var now = DateTime.UtcNow;

        var message = new DmMessageDatabaseEntity
        {
            PublicId = messagePublicId,
            ConversationId = conversation.Id,
            ConversationPublicId = conversationPublicId,
            SenderUserId = sender.Id,
            SenderUserPublicId = sender.PublicId,
            Content = Encrypt(content),
            CreatedAt = now
        };

        context.DmMessages.Add(message);

        conversation.LastMessageAt = now;
        conversation.LastMessageExcerpt = Encrypt(excerpt);
        // Un-hide conversation for both sides when a new message arrives
        conversation.HiddenFromInitiator = false;
        conversation.HiddenFromRecipient = false;
        context.DmConversations.Update(conversation);

        await context.SaveChangesAsync(ct);

        return new DmMessageDto(messagePublicId, senderPublicId, content, now, true);
    }

    public async Task MarkAsReadAsync(
        string conversationPublicId, string callerPublicId, CancellationToken ct = default)
    {
        var conv = await context.DmConversations
            .Where(c => c.PublicId == conversationPublicId &&
                        (c.InitiatorUserPublicId == callerPublicId || c.RecipientUserPublicId == callerPublicId))
            .FirstOrDefaultAsync(ct);

        if (conv is null) return;

        var callerId = conv.InitiatorUserPublicId == callerPublicId ? conv.InitiatorUserId : conv.RecipientUserId;
        var now = DateTime.UtcNow;

        var readState = await context.DmReadStates
            .FirstOrDefaultAsync(r => r.UserId == callerId && r.ConversationId == conv.Id, ct);

        if (readState is null)
        {
            context.DmReadStates.Add(new DmReadStateDatabaseEntity
            {
                UserId = callerId,
                ConversationId = conv.Id,
                LastReadAt = now
            });
        }
        else
        {
            readState.LastReadAt = now;
            context.DmReadStates.Update(readState);
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task<int> GetUnreadConversationCountAsync(
        string callerPublicId, CancellationToken ct = default)
    {
        var callerId = await context.Users
            .Where(u => u.PublicId == callerPublicId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (callerId == 0) return 0;

        var conversations = await context.DmConversations
            .Where(c => (c.InitiatorUserId == callerId || c.RecipientUserId == callerId) &&
                        c.LastMessageAt != null)
            .Select(c => new
            {
                c.Id,
                c.LastMessageAt,
                ReadState = c.ReadStates.FirstOrDefault(r => r.UserId == callerId)
            })
            .ToListAsync(ct);

        return conversations.Count(c =>
            c.ReadState is null ||
            c.ReadState.LastReadAt < c.LastMessageAt);
    }

    public async Task<(string RecipientPublicId, int RecipientIntId)?> GetRecipientInfoAsync(
        string conversationPublicId, string senderPublicId, CancellationToken ct = default)
    {
        var conv = await context.DmConversations
            .Where(c => c.PublicId == conversationPublicId)
            .Select(c => new
            {
                c.InitiatorUserId,
                c.InitiatorUserPublicId,
                c.RecipientUserId,
                c.RecipientUserPublicId
            })
            .FirstOrDefaultAsync(ct);

        if (conv is null) return null;

        if (conv.InitiatorUserPublicId == senderPublicId)
            return (conv.RecipientUserPublicId!, conv.RecipientUserId);
        return (conv.InitiatorUserPublicId!, conv.InitiatorUserId);
    }

    public async Task<int> GetUserIntIdAsync(string publicId, CancellationToken ct = default)
        => await context.Users
            .Where(u => u.PublicId == publicId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<string>> DeleteMessagesAsync(
        string conversationPublicId, string callerPublicId,
        IReadOnlyList<string> messagePublicIds, bool deleteForAll, CancellationToken ct = default)
    {
        var convId = await context.DmConversations
            .Where(c => c.PublicId == conversationPublicId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);

        if (convId is null) return [];

        var messages = await context.DmMessages
            .Where(m => m.ConversationId == convId.Value && messagePublicIds.Contains(m.PublicId))
            .ToListAsync(ct);

        if (messages.Count == 0) return [];

        if (deleteForAll)
        {
            // Only the caller's own messages are hard-deleted; others are hidden for the caller.
            var own = messages.Where(m => m.SenderUserPublicId == callerPublicId).ToList();
            var theirs = messages.Where(m => m.SenderUserPublicId != callerPublicId).ToList();

            if (own.Count > 0)
                context.DmMessages.RemoveRange(own);

            foreach (var m in theirs)
                m.HiddenFromRecipient = true;

            await context.SaveChangesAsync(ct);
            return own.Select(m => m.PublicId).ToList();
        }
        else
        {
            foreach (var m in messages)
            {
                if (m.SenderUserPublicId == callerPublicId)
                    m.HiddenFromSender = true;
                else
                    m.HiddenFromRecipient = true;
            }
            await context.SaveChangesAsync(ct);
            return [];
        }
    }

    public async Task<(bool Success, IReadOnlyList<string> HardDeletedIds)> DeleteConversationAsync(
        string conversationPublicId, string callerPublicId, bool deleteForAll, CancellationToken ct = default)
    {
        var conv = await context.DmConversations
            .Where(c => c.PublicId == conversationPublicId)
            .FirstOrDefaultAsync(ct);

        if (conv is null) return (false, []);

        var isInitiator = conv.InitiatorUserPublicId == callerPublicId;

        if (deleteForAll)
        {
            // Hard-delete only the caller's own messages; hide the other user's messages from caller.
            var hardDeletedIds = await context.DmMessages
                .Where(m => m.ConversationId == conv.Id && m.SenderUserPublicId == callerPublicId)
                .Select(m => m.PublicId)
                .ToListAsync(ct);

            await context.DmMessages
                .Where(m => m.ConversationId == conv.Id && m.SenderUserPublicId == callerPublicId)
                .ExecuteDeleteAsync(ct);

            // Hide the other user's messages from the caller's view
            if (isInitiator)
            {
                await context.DmMessages
                    .Where(m => m.ConversationId == conv.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.HiddenFromRecipient, true), ct);
                conv.HiddenFromInitiator = true;
            }
            else
            {
                await context.DmMessages
                    .Where(m => m.ConversationId == conv.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.HiddenFromSender, true), ct);
                conv.HiddenFromRecipient = true;
            }

            context.DmConversations.Update(conv);
            await context.SaveChangesAsync(ct);
            return (true, hardDeletedIds);
        }
        else
        {
            // Soft delete: hide all messages for caller AND hide the conversation
            if (isInitiator)
            {
                conv.HiddenFromInitiator = true;
                await context.DmMessages
                    .Where(m => m.ConversationId == conv.Id && m.SenderUserPublicId == callerPublicId)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.HiddenFromSender, true), ct);
                await context.DmMessages
                    .Where(m => m.ConversationId == conv.Id && m.SenderUserPublicId != callerPublicId)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.HiddenFromRecipient, true), ct);
            }
            else
            {
                conv.HiddenFromRecipient = true;
                await context.DmMessages
                    .Where(m => m.ConversationId == conv.Id && m.SenderUserPublicId == callerPublicId)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.HiddenFromSender, true), ct);
                await context.DmMessages
                    .Where(m => m.ConversationId == conv.Id && m.SenderUserPublicId != callerPublicId)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.HiddenFromRecipient, true), ct);
            }

            context.DmConversations.Update(conv);
            await context.SaveChangesAsync(ct);
            return (true, []);
        }
    }

    public async Task<bool> PinConversationAsync(
        string conversationPublicId, string callerPublicId, bool isPinned, CancellationToken ct = default)
    {
        var conv = await context.DmConversations
            .Where(c => c.PublicId == conversationPublicId)
            .FirstOrDefaultAsync(ct);

        if (conv is null) return false;

        if (conv.InitiatorUserPublicId == callerPublicId)
            conv.InitiatorPinned = isPinned;
        else
            conv.RecipientPinned = isPinned;

        context.DmConversations.Update(conv);
        await context.SaveChangesAsync(ct);
        return true;
    }
}
