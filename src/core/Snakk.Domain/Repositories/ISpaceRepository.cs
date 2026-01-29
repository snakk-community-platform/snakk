namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public interface ISpaceRepository
{
    Task<Space?> GetByIdAsync(int id);
    Task<Space?> GetByPublicIdAsync(SpaceId publicId);
    Task<Space?> GetBySlugAsync(string slug);
    Task<IEnumerable<Space>> GetAllAsync();
    Task<PagedResult<Space>> GetFilteredForDisplayAsync(HubId hubId, int offset, int pageSize);
    Task AddAsync(Space space);
    Task UpdateAsync(Space space);
}
