namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public interface IAnnouncementRepository
{
    Task<Announcement?> GetByPublicIdAsync(AnnouncementId publicId);
    Task<IEnumerable<Announcement>> GetByScopeAsync(AnnouncementScopeEnum scope, string scopeEntityId);
    Task<IEnumerable<Announcement>> GetActiveForCommunityAsync(CommunityId communityId);
    Task<IEnumerable<Announcement>> GetActiveForHubAsync(HubId hubId);
    Task<IEnumerable<Announcement>> GetActiveForSpaceAsync(SpaceId spaceId);
    Task AddAsync(Announcement announcement);
    Task UpdateAsync(Announcement announcement);
    Task DeleteAsync(AnnouncementId publicId);
}
