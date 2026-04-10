namespace Snakk.Infrastructure.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;
using Snakk.Domain.Repositories;
using Snakk.Shared.Helpers;
using System.IO;
using System.Text;
using System.Threading.Tasks;

public class AvatarGenerationService(
    IFileStorage fileStorage,
    IConfiguration configuration,
    ILogger<AvatarGenerationService> logger,
    IUserRepository userRepository,
    IHubRepository hubRepository,
    ISpaceRepository spaceRepository) : IAvatarGenerationService
{
    private readonly int _defaultSize = configuration.GetValue<int>("AvatarSettings:DefaultSize", 80);

    public async Task<string> GenerateUserAvatarAsync(string userId, int size = 80)
    {
        var relativePath = GetAvatarRelativePath("users", userId);

        // Skip if file already exists
        if (await fileStorage.ExistsAsync(relativePath))
        {
            logger.LogDebug("Avatar already exists for user {UserId}, skipping generation", userId);
            return fileStorage.GetPublicUrl(relativePath);
        }

        // Generate SVG using the existing AvatarGenerator
        var svg = Snakk.Shared.Avatars.AvatarGenerator.Generate(userId, size);

        // Write SVG to storage
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        await fileStorage.SaveAsync(relativePath, stream, "public, max-age=31536000, immutable");

        logger.LogInformation("Generated avatar for user {UserId} at {RelativePath}", userId, relativePath);

        return fileStorage.GetPublicUrl(relativePath);
    }

    public async Task<string> GenerateHubAvatarAsync(string hubId, int size = 80)
    {
        var relativePath = GetAvatarRelativePath("hubs", hubId);

        if (await fileStorage.ExistsAsync(relativePath))
        {
            logger.LogDebug("Avatar already exists for hub {HubId}, skipping generation", hubId);
            return fileStorage.GetPublicUrl(relativePath);
        }

        // Generate SVG with "hub:" prefix for proper seeding
        var svg = Snakk.Shared.Avatars.AvatarGenerator.Generate($"hub:{hubId}", size);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        await fileStorage.SaveAsync(relativePath, stream, "public, max-age=31536000, immutable");

        logger.LogInformation("Generated avatar for hub {HubId} at {RelativePath}", hubId, relativePath);

        return fileStorage.GetPublicUrl(relativePath);
    }

    public async Task<string> GenerateSpaceAvatarAsync(string spaceId, int size = 80)
    {
        var relativePath = GetAvatarRelativePath("spaces", spaceId);

        if (await fileStorage.ExistsAsync(relativePath))
        {
            logger.LogDebug("Avatar already exists for space {SpaceId}, skipping generation", spaceId);
            return fileStorage.GetPublicUrl(relativePath);
        }

        // Generate SVG with "space:" prefix for proper seeding
        var svg = Snakk.Shared.Avatars.AvatarGenerator.Generate($"space:{spaceId}", size);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        await fileStorage.SaveAsync(relativePath, stream, "public, max-age=31536000, immutable");

        logger.LogInformation("Generated avatar for space {SpaceId} at {RelativePath}", spaceId, relativePath);

        return fileStorage.GetPublicUrl(relativePath);
    }

    public async Task<string> GenerateCommunityAvatarAsync(string communityId, int size = 80)
    {
        var relativePath = GetAvatarRelativePath("communities", communityId);

        if (await fileStorage.ExistsAsync(relativePath))
        {
            logger.LogDebug("Avatar already exists for community {CommunityId}, skipping generation", communityId);
            return fileStorage.GetPublicUrl(relativePath);
        }

        // Generate SVG with "community:" prefix for proper seeding
        var svg = Snakk.Shared.Avatars.AvatarGenerator.Generate($"community:{communityId}", size);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        await fileStorage.SaveAsync(relativePath, stream, "public, max-age=31536000, immutable");

        logger.LogInformation("Generated avatar for community {CommunityId} at {RelativePath}", communityId, relativePath);

        return fileStorage.GetPublicUrl(relativePath);
    }

    public async Task<bool> AvatarExistsAsync(string entityType, string entityId)
    {
        var relativePath = GetAvatarRelativePath(entityType, entityId);
        return await fileStorage.ExistsAsync(relativePath);
    }

    public async Task DeleteAvatarAsync(string entityType, string entityId)
    {
        var relativePath = GetAvatarRelativePath(entityType, entityId);

        if (await fileStorage.ExistsAsync(relativePath))
        {
            await fileStorage.DeleteAsync(relativePath);
            logger.LogInformation("Deleted avatar for {EntityType} {EntityId}", entityType, entityId);
        }
    }

    public async Task<int> GenerateAllMissingAvatarsAsync()
    {
        logger.LogInformation("Starting generation of all missing avatars...");
        var totalGenerated = 0;

        try
        {
            // Generate user avatars
            var users = await userRepository.GetAllAsync();
            var usersList = users.ToList();
            logger.LogInformation("Found {Count} users to process", usersList.Count);

            var userTasks = usersList.Select(async user =>
            {
                try
                {
                    var relativePath = GetAvatarRelativePath("users", user.PublicId.Value);

                    if (!await fileStorage.ExistsAsync(relativePath))
                    {
                        await GenerateUserAvatarAsync(user.PublicId.Value, _defaultSize);
                        return 1;
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to generate avatar for user {UserId}", user.PublicId.Value);
                    return 0;
                }
            });

            var userResults = await Task.WhenAll(userTasks);
            var usersGenerated = userResults.Sum();

            totalGenerated += usersGenerated;
            logger.LogInformation("Generated {Count} user avatars", usersGenerated);

            // Generate hub avatars
            var hubs = await hubRepository.GetAllAsync();
            var hubsList = hubs.ToList();
            logger.LogInformation("Found {Count} hubs to process", hubsList.Count);

            var hubTasks = hubsList.Select(async hub =>
            {
                try
                {
                    var relativePath = GetAvatarRelativePath("hubs", hub.PublicId.Value);

                    if (!await fileStorage.ExistsAsync(relativePath))
                    {
                        await GenerateHubAvatarAsync(hub.PublicId.Value, _defaultSize);
                        return 1;
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to generate avatar for hub {HubId}", hub.PublicId.Value);
                    return 0;
                }
            });

            var hubResults = await Task.WhenAll(hubTasks);
            var hubsGenerated = hubResults.Sum();

            totalGenerated += hubsGenerated;
            logger.LogInformation("Generated {Count} hub avatars", hubsGenerated);

            // Generate space avatars
            var spaces = await spaceRepository.GetAllAsync();
            var spacesList = spaces.ToList();
            logger.LogInformation("Found {Count} spaces to process", spacesList.Count);

            var spaceTasks = spacesList.Select(async space =>
            {
                try
                {
                    var relativePath = GetAvatarRelativePath("spaces", space.PublicId.Value);

                    if (!await fileStorage.ExistsAsync(relativePath))
                    {
                        await GenerateSpaceAvatarAsync(space.PublicId.Value, _defaultSize);
                        return 1;
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to generate avatar for space {SpaceId}", space.PublicId.Value);
                    return 0;
                }
            });

            var spaceResults = await Task.WhenAll(spaceTasks);
            var spacesGenerated = spaceResults.Sum();

            totalGenerated += spacesGenerated;
            logger.LogInformation("Generated {Count} space avatars", spacesGenerated);

            logger.LogInformation("Avatar generation complete. Total generated: {Total}", totalGenerated);

            return totalGenerated;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during avatar generation");
            throw;
        }
    }

    private string GetAvatarRelativePath(string entityType, string entityId, int revision = 0)
    {
        // Map entity type string to enum
        var avatarEntityType = entityType.ToLowerInvariant() switch
        {
            "user" or "users" => AvatarEntityType.User,
            "community" or "communities" => AvatarEntityType.Community,
            "hub" or "hubs" => AvatarEntityType.Hub,
            "space" or "spaces" => AvatarEntityType.Space,
            _ => throw new ArgumentException($"Unknown entity type: {entityType}")
        };

        return AvatarHelper.GetFullRelativePath(entityId, avatarEntityType, revision);
    }
}
