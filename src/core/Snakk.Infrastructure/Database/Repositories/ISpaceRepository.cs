namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public interface ISpaceRepository : IGenericDatabaseRepository<SpaceDatabaseEntity>
{
    Task<SpaceDatabaseEntity?> GetForUpdateAsync(string publicId, CancellationToken ct = default);
    Task<SpaceDetailDto?> GetForDisplayAsync(string publicId, CancellationToken ct = default);
    Task<SpaceDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default);
    Task<SpaceDatabaseEntity?> GetBySlugAsync(string slug, string hubSlug, CancellationToken ct = default);
    Task<PagedResult<SpaceRepository.SpaceListDto>> GetFilteredForDisplayAsync(string hubPublicId, int offset, int pageSize, CancellationToken ct = default);
}

public record SpaceListDto(
    string PublicId,
    string Name,
    string Slug,
    string? Description,
    bool AllowAnonymousReading,
    bool RequireEmailConfirmation,
    DateTime CreatedAt,
    string HubPublicId,
    string HubName);

public record SpaceDetailDto(
    string PublicId,
    string Name,
    string Slug,
    string? Description,
    bool AllowAnonymousReading,
    bool RequireEmailConfirmation,
    DateTime CreatedAt,
    string HubPublicId,
    string HubName);
