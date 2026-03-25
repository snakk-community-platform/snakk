namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public interface IHubRepository
{
    Task<Hub?> GetByIdAsync(int id);
    Task<Hub?> GetByPublicIdAsync(HubId publicId);
    Task<Hub?> GetBySlugAsync(string slug, string communitySlug);
    Task<IEnumerable<Hub>> GetAllAsync();
    Task<PagedResult<Hub>> GetFilteredForDisplayAsync(int offset, int pageSize);
    Task<PagedResult<Hub>> GetByCommunityAsync(CommunityId communityId, int offset, int pageSize, string? userId = null);
    Task AddAsync(Hub hub);
    Task UpdateAsync(Hub hub);
}
