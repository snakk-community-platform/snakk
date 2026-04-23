namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class UserSocialLinkRepository(SnakkDbContext context) : IUserSocialLinkRepository
{
    public async Task<List<(string Platform, string Username)>> GetByUserPublicIdAsync(string publicId)
    {
        return await context.UserSocialLinks
            .Where(l => l.User.PublicId == publicId && !l.User.IsDeleted)
            .OrderBy(l => l.Platform)
            .Select(l => ValueTuple.Create(l.Platform, l.Username))
            .ToListAsync();
    }

    public async Task<List<(string Platform, string Username)>> GetByUserInternalIdAsync(int userId)
    {
        return await context.UserSocialLinks
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.Platform)
            .Select(l => ValueTuple.Create(l.Platform, l.Username))
            .ToListAsync();
    }

    public async Task ReplaceAllAsync(int userId, List<(string Platform, string Username)> links)
    {
        var existing = await context.UserSocialLinks
            .Where(l => l.UserId == userId)
            .ToListAsync();

        context.UserSocialLinks.RemoveRange(existing);

        foreach (var (platform, username) in links)
        {
            context.UserSocialLinks.Add(new UserSocialLinkDatabaseEntity
            {
                UserId = userId,
                Platform = platform,
                Username = username,
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task<int?> GetUserInternalIdByPublicIdAsync(string publicId)
    {
        return await context.Users
            .Where(u => u.PublicId == publicId && !u.IsDeleted)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();
    }
}
