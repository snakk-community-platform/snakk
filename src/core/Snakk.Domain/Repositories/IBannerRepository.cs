namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public interface IBannerRepository
{
    Task<Banner?> GetByPublicIdAsync(BannerId publicId, CancellationToken ct = default);
    Task<IEnumerable<Banner>> GetByScopeAsync(BannerScopeEnum scope, string scopeEntityId, CancellationToken ct = default);
    Task<IEnumerable<Banner>> GetActiveForCommunityAsync(CommunityId communityId, CancellationToken ct = default);
    Task<IEnumerable<Banner>> GetActiveForHubAsync(HubId hubId, CancellationToken ct = default);
    Task<IEnumerable<Banner>> GetActiveForSpaceAsync(SpaceId spaceId, CancellationToken ct = default);
    Task AddAsync(Banner banner, CancellationToken ct = default);
    Task UpdateAsync(Banner banner, CancellationToken ct = default);
    Task DeleteAsync(BannerId publicId, CancellationToken ct = default);
}
