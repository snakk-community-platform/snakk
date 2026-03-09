using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

namespace Snakk.Infrastructure.Services;

public class AuthorizationService(
    SnakkDbContext context,
    ILogger<AuthorizationService> _logger) : IAuthorizationService
{
    public async Task<bool> UserHas2FAEnabledAsync(string userId)
    {
        var user = await context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new { u.TwoFactorEnabled })
            .FirstOrDefaultAsync();

        return user?.TwoFactorEnabled ?? false;
    }
}
