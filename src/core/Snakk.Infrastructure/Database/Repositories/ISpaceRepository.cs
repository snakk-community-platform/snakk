namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public interface ISpaceRepository : IGenericDatabaseRepository<SpaceDatabaseEntity>
{
    Task<SpaceDatabaseEntity?> GetForUpdateAsync(string publicId);
    Task<SpaceDetailDto?> GetForDisplayAsync(string publicId);
    Task<SpaceDatabaseEntity?> GetByPublicIdAsync(string publicId);
    Task<SpaceDatabaseEntity?> GetBySlugAsync(string slug);
    Task<PagedResult<SpaceRepository.SpaceListDto>> GetFilteredForDisplayAsync(string hubPublicId, int offset, int pageSize);
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
