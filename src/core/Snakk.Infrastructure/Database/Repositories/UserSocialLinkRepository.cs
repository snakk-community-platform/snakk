namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class UserSocialLinkRepository(SnakkDbContext context) : IUserSocialLinkRepository
{
    public async Task<List<(string Platform, string Username)>> GetByUserPublicIdAsync(string publicId, CancellationToken ct = default)
    {
        return await context.UserSocialLinks
            .Where(l => l.UserPublicId == publicId)
            .OrderBy(l => l.Platform)
            .Select(l => ValueTuple.Create(l.Platform, l.Username))
            .ToListAsync(ct);
    }

    public async Task<List<(string Platform, string Username)>> GetByUserInternalIdAsync(int userId, CancellationToken ct = default)
    {
        return await context.UserSocialLinks
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.Platform)
            .Select(l => ValueTuple.Create(l.Platform, l.Username))
            .ToListAsync(ct);
    }

    public async Task ReplaceAllAsync(int userId, List<(string Platform, string Username)> links, CancellationToken ct = default)
    {
        var existing = await context.UserSocialLinks
            .Where(l => l.UserId == userId)
            .ToListAsync(ct);

        context.UserSocialLinks.RemoveRange(existing);

        var userPublicId = await context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.PublicId)
            .FirstOrDefaultAsync(ct);

        foreach (var (platform, username) in links)
        {
            context.UserSocialLinks.Add(new UserSocialLinkDatabaseEntity
            {
                UserId = userId,
                UserPublicId = userPublicId,
                Platform = platform,
                Username = username,
            });
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task<int?> GetUserInternalIdByPublicIdAsync(string publicId, CancellationToken ct = default)
    {
        return await context.Users
            .Where(u => u.PublicId == publicId && !u.IsDeleted)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);
    }
}
