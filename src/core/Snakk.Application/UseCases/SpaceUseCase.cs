namespace Snakk.Application.UseCases;

using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public class SpaceUseCase(
    ISpaceRepository spaceRepository,
    IHubRepository hubRepository) : UseCaseBase
{
    public async Task<Result<Space>> CreateSpaceAsync(
        HubId hubId,
        string name,
        string slug,
        string? description = null)
    {
        // Validate hub exists
        var hub = await hubRepository.GetByPublicIdAsync(hubId);

        if (hub is null)
            return Result<Space>.Failure($"Hub '{hubId}' not found");

        // Create space
        var space = Space.Create(hubId, name, slug, description);

        // Persist
        await spaceRepository.AddAsync(space);

        return Result<Space>.Success(space);
    }

    public async Task<Result<Space>> GetSpaceAsync(SpaceId spaceId)
    {
        var space = await spaceRepository.GetByPublicIdAsync(spaceId);

        if (space is null)
            return Result<Space>.Failure($"Space '{spaceId}' not found");

        return Result<Space>.Success(space);
    }

    public async Task<Result<Space>> GetSpaceBySlugAsync(string slug, string hubSlug)
    {
        var space = await spaceRepository.GetBySlugAsync(slug, hubSlug);

        if (space is null)
            return Result<Space>.Failure($"Space with slug '{slug}' not found");

        return Result<Space>.Success(space);
    }

    public async Task<PagedResult<Space>> GetSpacesByHubAsync(HubId hubId, int offset = 0, int pageSize = 20) =>
        await spaceRepository.GetFilteredForDisplayAsync(hubId, offset, pageSize);

    public async Task<Result<Space>> UpdateSpaceNameAsync(
        SpaceId spaceId,
        string newName)
    {
        var space = await spaceRepository.GetByPublicIdAsync(spaceId);

        if (space is null)
            return Result<Space>.Failure($"Space '{spaceId}' not found");

        space.UpdateName(newName);
        await spaceRepository.UpdateAsync(space);

        return Result<Space>.Success(space);
    }

    public async Task<Result<Space>> UpdateSpaceDescriptionAsync(
        SpaceId spaceId,
        string? newDescription)
    {
        var space = await spaceRepository.GetByPublicIdAsync(spaceId);

        if (space is null)
            return Result<Space>.Failure($"Space '{spaceId}' not found");

        space.UpdateDescription(newDescription);
        await spaceRepository.UpdateAsync(space);

        return Result<Space>.Success(space);
    }

    public async Task<Result<Space>> UpdateSpaceSlugAsync(
        SpaceId spaceId,
        string newSlug)
    {
        var space = await spaceRepository.GetByPublicIdAsync(spaceId);

        if (space is null)
            return Result<Space>.Failure($"Space '{spaceId}' not found");

        space.UpdateSlug(newSlug);
        await spaceRepository.UpdateAsync(space);

        return Result<Space>.Success(space);
    }
}
