using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

namespace Snakk.Infrastructure.Services;

public class AuthorizationService(
    SnakkDbContext context) : IAuthorizationService
{
    public async Task<bool> UserHas2FAEnabledAsync(string userId, CancellationToken ct = default)
    {
        var user = await context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new { u.TwoFactorEnabled })
            .FirstOrDefaultAsync(ct);

        return user?.TwoFactorEnabled ?? false;
    }
}
