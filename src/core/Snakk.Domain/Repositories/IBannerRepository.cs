namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public interface IBannerRepository
{
    Task<Banner?> GetByPublicIdAsync(BannerId publicId);
    Task<IEnumerable<Banner>> GetByScopeAsync(BannerScopeEnum scope, string scopeEntityId);
    Task<IEnumerable<Banner>> GetActiveForCommunityAsync(CommunityId communityId);
    Task<IEnumerable<Banner>> GetActiveForHubAsync(HubId hubId);
    Task<IEnumerable<Banner>> GetActiveForSpaceAsync(SpaceId spaceId);
    Task AddAsync(Banner banner);
    Task UpdateAsync(Banner banner);
    Task DeleteAsync(BannerId publicId);
}
