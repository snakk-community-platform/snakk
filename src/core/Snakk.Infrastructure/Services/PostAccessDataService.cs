namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

public class PostAccessDataService(SnakkDbContext dbContext) : IPostAccessDataService
{
    public async Task<bool?> IsPostRestrictedAsync(string publicId, CancellationToken ct = default)
    {
        var result = await dbContext.Posts
            .Where(p => p.PublicId == publicId && !p.IsDeleted)
            .Select(p => (bool?)(p.Discussion.Space.IsRestricted
                || p.Discussion.Space.Hub.IsRestricted
                || p.Discussion.Space.Hub.Community.IsRestricted))
            .FirstOrDefaultAsync(ct);

        return result;
    }
}
