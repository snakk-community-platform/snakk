namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public interface ICommunityRepository
{
    Task<Community?> GetByPublicIdAsync(CommunityId publicId);
    Task<Community?> GetBySlugAsync(string slug);
    Task<Community?> GetByDomainAsync(string domain);
    Task<PagedResult<Community>> GetPublicListedAsync(int offset, int pageSize);
    Task<PagedResult<Community>> GetForPlatformFeedAsync(int offset, int pageSize);
    Task AddAsync(Community community);
    Task UpdateAsync(Community community);
}
