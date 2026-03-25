namespace Snakk.Application.UseCases;

using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public class HubUseCase(
    IHubRepository hubRepository,
    ICommunityRepository communityRepository) : UseCaseBase
{
    public async Task<Result<Hub>> CreateHubAsync(
        CommunityId communityId,
        string name,
        string slug,
        string? description = null)
    {
        // Verify community exists
        var community = await communityRepository.GetByPublicIdAsync(communityId);

        if (community is null)
            return Result<Hub>.Failure($"Community '{communityId}' not found");

        // Create hub
        var hub = Hub.Create(communityId, name, slug, description);

        // Persist
        await hubRepository.AddAsync(hub);

        return Result<Hub>.Success(hub);
    }

    public async Task<Result<Hub>> GetHubAsync(HubId hubId)
    {
        var hub = await hubRepository.GetByPublicIdAsync(hubId);

        if (hub is null)
            return Result<Hub>.Failure($"Hub '{hubId}' not found");

        return Result<Hub>.Success(hub);
    }

    public async Task<Result<Hub>> GetHubBySlugAsync(string slug, string communitySlug)
    {
        var hub = await hubRepository.GetBySlugAsync(slug, communitySlug);

        if (hub is null)
            return Result<Hub>.Failure($"Hub with slug '{slug}' not found");

        return Result<Hub>.Success(hub);
    }

    public async Task<PagedResult<Hub>> GetAllHubsAsync(int offset = 0, int pageSize = 20) =>
        await hubRepository.GetFilteredForDisplayAsync(offset, pageSize);

    public async Task<PagedResult<Hub>> GetHubsByCommunityAsync(CommunityId communityId, int offset = 0, int pageSize = 20, string? userId = null) =>
        await hubRepository.GetByCommunityAsync(communityId, offset, pageSize, userId);

    public async Task<Result<Hub>> UpdateHubNameAsync(
        HubId hubId,
        string newName)
    {
        var hub = await hubRepository.GetByPublicIdAsync(hubId);

        if (hub is null)
            return Result<Hub>.Failure($"Hub '{hubId}' not found");

        hub.UpdateName(newName);
        await hubRepository.UpdateAsync(hub);

        return Result<Hub>.Success(hub);
    }

    public async Task<Result<Hub>> UpdateHubDescriptionAsync(
        HubId hubId,
        string? newDescription)
    {
        var hub = await hubRepository.GetByPublicIdAsync(hubId);

        if (hub is null)
            return Result<Hub>.Failure($"Hub '{hubId}' not found");

        hub.UpdateDescription(newDescription);
        await hubRepository.UpdateAsync(hub);

        return Result<Hub>.Success(hub);
    }

    public async Task<Result<Hub>> UpdateHubSlugAsync(
        HubId hubId,
        string newSlug)
    {
        var hub = await hubRepository.GetByPublicIdAsync(hubId);

        if (hub is null)
            return Result<Hub>.Failure($"Hub '{hubId}' not found");

        hub.UpdateSlug(newSlug);
        await hubRepository.UpdateAsync(hub);

        return Result<Hub>.Success(hub);
    }
}
