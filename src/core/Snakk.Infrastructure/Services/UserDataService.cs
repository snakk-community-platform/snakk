namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

public class UserDataService(SnakkDbContext dbContext) : IUserDataService
{
    public async Task<UserDiscordInfoDto?> GetDiscordInfoAsync(string publicId, CancellationToken ct = default)
    {
        var result = await dbContext.Users
            .Where(u => u.PublicId == publicId && u.DiscordUserId != null)
            .Select(u => new { u.DiscordUserId, u.DiscordUsername })
            .FirstOrDefaultAsync(ct);

        return result is null
            ? null
            : new UserDiscordInfoDto(result.DiscordUserId!, result.DiscordUsername);
    }
}
