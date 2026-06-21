namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class DisplayNameHistoryRepository(SnakkDbContext context) : IDisplayNameHistoryRepository
{
    public async Task AddAsync(string userPublicId, string previousName, string newName, string? changedByUserPublicId = null, CancellationToken ct = default)
    {
        var userId = await context.Users
            .Where(u => u.PublicId == userPublicId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (userId == 0) return;

        int? changedByUserId = null;
        if (changedByUserPublicId is not null)
        {
            changedByUserId = await context.Users
                .Where(u => u.PublicId == changedByUserPublicId)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(ct);
        }

        context.UserDisplayNameHistories.Add(new UserDisplayNameHistoryDatabaseEntity
        {
            UserId = userId,
            PreviousName = previousName,
            NewName = newName,
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = changedByUserId
        });

        await context.SaveChangesAsync(ct);
    }

    public async Task<bool> WasNameEverUsedAsync(string displayName, string? excludeUserPublicId = null, CancellationToken ct = default)
        => await context.UserDisplayNameHistories
            .AnyAsync(h =>
                (h.NewName.ToLower() == displayName.ToLower()
                 || h.PreviousName.ToLower() == displayName.ToLower())
                && (excludeUserPublicId == null || h.User.PublicId != excludeUserPublicId), ct);

    public async Task<List<DisplayNameHistoryDto>> GetHistoryForUserAsync(string userPublicId, int limit = 20, CancellationToken ct = default)
        => await context.UserDisplayNameHistories
            .Where(h => h.User.PublicId == userPublicId)
            .OrderByDescending(h => h.ChangedAt)
            .Take(limit)
            .Select(h => new DisplayNameHistoryDto(
                h.PreviousName,
                h.NewName,
                h.ChangedAt,
                h.ChangedByUser != null ? h.ChangedByUser.PublicId : null))
            .ToListAsync(ct);
}
