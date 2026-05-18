namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public interface IHubRepository
{
    Task<Hub?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Hub?> GetByPublicIdAsync(HubId publicId, CancellationToken ct = default);
    Task<Hub?> GetBySlugAsync(string slug, string communitySlug, CancellationToken ct = default);
    Task<IEnumerable<Hub>> GetAllAsync(CancellationToken ct = default);
    Task<PagedResult<Hub>> GetFilteredForDisplayAsync(int offset, int pageSize, CancellationToken ct = default);
    Task<PagedResult<Hub>> GetByCommunityAsync(CommunityId communityId, int offset, int pageSize, string? userId = null, CancellationToken ct = default);
    Task AddAsync(Hub hub, CancellationToken ct = default);
    Task UpdateAsync(Hub hub, CancellationToken ct = default);
}
