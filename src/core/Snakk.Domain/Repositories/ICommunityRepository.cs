namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public interface ICommunityRepository
{
    Task<Community?> GetByPublicIdAsync(CommunityId publicId, CancellationToken ct = default);
    Task<Community?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Community?> GetByDomainAsync(string domain, CancellationToken ct = default);
    Task<PagedResult<Community>> GetPublicListedAsync(int offset, int pageSize, CancellationToken ct = default);
    Task<PagedResult<Community>> GetForPlatformFeedAsync(int offset, int pageSize, CancellationToken ct = default);
    Task AddAsync(Community community, CancellationToken ct = default);
    Task UpdateAsync(Community community, CancellationToken ct = default);
}
