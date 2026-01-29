namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public interface IHubRepository : IGenericDatabaseRepository<HubDatabaseEntity>
{
    Task<HubDatabaseEntity?> GetForUpdateAsync(string publicId);
    Task<HubDetailDto?> GetForDisplayAsync(string publicId);
    Task<HubDatabaseEntity?> GetByPublicIdAsync(string publicId);
    Task<HubDatabaseEntity?> GetBySlugAsync(string slug);
    Task<PagedResult<HubRepository.HubListDto>> GetFilteredForDisplayAsync(int offset, int pageSize);
    Task<PagedResult<HubRepository.HubListDto>> GetByCommunityAsync(int communityId, int offset, int pageSize);
    Task<int?> GetCommunityDbIdAsync(string communityPublicId);
}

public record HubListDto(
    string PublicId,
    string CommunityPublicId,
    string Name,
    string Slug,
    string? Description,
    bool AllowAnonymousReading,
    bool RequireEmailConfirmation,
    DateTime CreatedAt);

public record HubDetailDto(
    string PublicId,
    string CommunityPublicId,
    string Name,
    string Slug,
    string? Description,
    bool AllowAnonymousReading,
    bool RequireEmailConfirmation,
    DateTime CreatedAt);
