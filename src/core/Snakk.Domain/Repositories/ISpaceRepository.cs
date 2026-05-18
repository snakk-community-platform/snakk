namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public interface ISpaceRepository
{
    Task<Space?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Space?> GetByPublicIdAsync(SpaceId publicId, CancellationToken ct = default);
    Task<Space?> GetBySlugAsync(string slug, string hubSlug, CancellationToken ct = default);
    Task<IEnumerable<Space>> GetAllAsync(CancellationToken ct = default);
    Task<PagedResult<Space>> GetFilteredForDisplayAsync(HubId hubId, int offset, int pageSize, CancellationToken ct = default);
    Task AddAsync(Space space, CancellationToken ct = default);
    Task UpdateAsync(Space space, CancellationToken ct = default);
}
