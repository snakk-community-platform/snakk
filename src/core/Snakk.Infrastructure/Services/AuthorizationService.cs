using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

namespace Snakk.Infrastructure.Services;

public class AuthorizationService : IAuthorizationService
{
    private readonly SnakkDbContext _context;
    private readonly ILogger<AuthorizationService> _logger;

    public AuthorizationService(
        SnakkDbContext context,
        ILogger<AuthorizationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> UserHas2FAEnabledAsync(string userId)
    {
        var user = await _context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new { u.TwoFactorEnabled })
            .FirstOrDefaultAsync();

        return user?.TwoFactorEnabled ?? false;
    }
}
