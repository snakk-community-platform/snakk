namespace Snakk.Application.UseCases;

using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public class CommunityUseCase(
    ICommunityRepository communityRepository) : UseCaseBase
{
    public async Task<Result<Community>> CreateCommunityAsync(
        string name,
        string slug,
        string? description = null,
        CommunityVisibility visibility = CommunityVisibility.PublicListed,
        bool exposeToPlatformFeed = true)
    {
        // Check if slug is already taken
        var existing = await communityRepository.GetBySlugAsync(slug);

        if (existing is not null)
            return Result<Community>.Failure($"Community with slug '{slug}' already exists");

        // Create community
        var community = Community.Create(name, slug, description, visibility, exposeToPlatformFeed);

        // Persist
        await communityRepository.AddAsync(community);

        return Result<Community>.Success(community);
    }

    public async Task<Result<Community>> GetCommunityAsync(CommunityId communityId)
    {
        var community = await communityRepository.GetByPublicIdAsync(communityId);

        if (community is null)
            return Result<Community>.Failure($"Community '{communityId}' not found");

        return Result<Community>.Success(community);
    }

    public async Task<Result<Community>> GetCommunityBySlugAsync(string slug)
    {
        var community = await communityRepository.GetBySlugAsync(slug);

        if (community is null)
            return Result<Community>.Failure($"Community with slug '{slug}' not found");

        return Result<Community>.Success(community);
    }

    public async Task<Result<Community>> GetCommunityByDomainAsync(string domain)
    {
        var community = await communityRepository.GetByDomainAsync(domain);

        if (community is null)
            return Result<Community>.Failure($"Community with domain '{domain}' not found");

        return Result<Community>.Success(community);
    }

    public async Task<PagedResult<Community>> GetPublicCommunitiesAsync(int offset = 0, int pageSize = 20) =>
        await communityRepository.GetPublicListedAsync(offset, pageSize);

    public async Task<PagedResult<Community>> GetCommunitiesForPlatformFeedAsync(int offset = 0, int pageSize = 20) =>
        await communityRepository.GetForPlatformFeedAsync(offset, pageSize);

    public async Task<Result<Community>> UpdateCommunityNameAsync(
        CommunityId communityId,
        string newName)
    {
        var community = await communityRepository.GetByPublicIdAsync(communityId);

        if (community is null)
            return Result<Community>.Failure($"Community '{communityId}' not found");

        community.UpdateName(newName);
        await communityRepository.UpdateAsync(community);

        return Result<Community>.Success(community);
    }

    public async Task<Result<Community>> UpdateCommunityDescriptionAsync(
        CommunityId communityId,
        string? newDescription)
    {
        var community = await communityRepository.GetByPublicIdAsync(communityId);

        if (community is null)
            return Result<Community>.Failure($"Community '{communityId}' not found");

        community.UpdateDescription(newDescription);
        await communityRepository.UpdateAsync(community);

        return Result<Community>.Success(community);
    }

    public async Task<Result<Community>> UpdateCommunityVisibilityAsync(
        CommunityId communityId,
        CommunityVisibility visibility)
    {
        var community = await communityRepository.GetByPublicIdAsync(communityId);

        if (community is null)
            return Result<Community>.Failure($"Community '{communityId}' not found");

        community.UpdateVisibility(visibility);
        await communityRepository.UpdateAsync(community);

        return Result<Community>.Success(community);
    }

    public async Task<Result<Community>> SetExposeToPlatformFeedAsync(
        CommunityId communityId,
        bool expose)
    {
        var community = await communityRepository.GetByPublicIdAsync(communityId);

        if (community is null)
            return Result<Community>.Failure($"Community '{communityId}' not found");

        community.SetExposeToPlatformFeed(expose);
        await communityRepository.UpdateAsync(community);

        return Result<Community>.Success(community);
    }
}
