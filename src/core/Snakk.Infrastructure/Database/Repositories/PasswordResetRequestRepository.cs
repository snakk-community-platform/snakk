namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

public class PasswordResetRequestRepository(SnakkDbContext context) : IPasswordResetRequestRepository
{
    public async Task LogAsync(
        string emailHash,
        string ipAddress,
        PasswordResetRequestOutcomeEnum outcome)
    {
        var entity = new PasswordResetRequestDatabaseEntity
        {
            EmailHash = emailHash,
            IpAddress = ipAddress,
            RequestedAt = DateTime.UtcNow,
            Outcome = (int)outcome
        };

        await context.PasswordResetRequests.AddAsync(entity);
        await context.SaveChangesAsync();
    }

    public async Task<int> CountByIpInWindowAsync(string ipAddress, TimeSpan window)
    {
        var since = DateTime.UtcNow - window;
        return await context.PasswordResetRequests
            .CountAsync(r =>
                r.IpAddress == ipAddress
                && r.RequestedAt >= since);
    }
}
