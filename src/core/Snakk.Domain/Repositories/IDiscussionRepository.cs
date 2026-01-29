namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public interface IDiscussionRepository
{
    Task<Discussion?> GetByIdAsync(int id);
    Task<Discussion?> GetByPublicIdAsync(DiscussionId publicId);
    Task<Discussion?> GetBySlugAsync(string slug);
    Task<IEnumerable<Discussion>> GetBySpaceIdAsync(SpaceId spaceId);
    Task<PagedResult<Discussion>> GetBySpaceIdAsync(SpaceId spaceId, int offset, int pageSize);
    Task<PagedResult<Discussion>> GetPagedBySpaceIdAsync(SpaceId spaceId, int offset, int pageSize);
    Task<IEnumerable<Discussion>> GetRecentAsync(int count = 10);
    Task AddAsync(Discussion discussion);
    Task UpdateAsync(Discussion discussion);
}
