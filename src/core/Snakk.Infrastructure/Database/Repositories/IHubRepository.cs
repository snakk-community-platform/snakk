namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public interface IHubRepository : IGenericDatabaseRepository<HubDatabaseEntity>
{
    Task<HubDatabaseEntity?> GetForUpdateAsync(string publicId, CancellationToken ct = default);
    Task<HubDetailDto?> GetForDisplayAsync(string publicId, CancellationToken ct = default);
    Task<HubDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default);
    Task<HubDatabaseEntity?> GetBySlugAsync(string slug, string communitySlug, CancellationToken ct = default);
    Task<PagedResult<HubRepository.HubListDto>> GetFilteredForDisplayAsync(int offset, int pageSize, CancellationToken ct = default);
    Task<PagedResult<HubRepository.HubListDto>> GetByCommunityAsync(int communityId, int offset, int pageSize, string? userId = null, CancellationToken ct = default);
    Task<int?> GetCommunityDbIdAsync(string communityPublicId, CancellationToken ct = default);
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
