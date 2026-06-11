namespace Snakk.Infrastructure.Database.Extensions;

using Microsoft.EntityFrameworkCore;
using Snakk.Shared.Models;

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var items = await query.Skip(offset).Take(pageSize + 1).ToListAsync(ct);
        var hasMore = items.Count > pageSize;
        return new PagedResult<T>
        {
            Items = hasMore ? items.Take(pageSize).ToList() : items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMore
        };
    }
}
