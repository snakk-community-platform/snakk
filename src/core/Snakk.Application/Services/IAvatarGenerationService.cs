namespace Snakk.Application.Services;

public interface IAvatarGenerationService
{
    /// <summary>
    /// Generates an avatar SVG file for a user and saves it to disk.
    /// </summary>
    /// <param name="userId">The user's public ID</param>
    /// <param name="size">The size of the avatar (default: 80)</param>
    /// <returns>The file path of the generated avatar</returns>
    Task<string> GenerateUserAvatarAsync(string userId, int size = 80);

    /// <summary>
    /// Generates an avatar SVG file for a hub and saves it to disk.
    /// </summary>
    /// <param name="hubId">The hub's public ID</param>
    /// <param name="size">The size of the avatar (default: 80)</param>
    /// <returns>The file path of the generated avatar</returns>
    Task<string> GenerateHubAvatarAsync(string hubId, int size = 80);

    /// <summary>
    /// Generates an avatar SVG file for a space and saves it to disk.
    /// </summary>
    /// <param name="spaceId">The space's public ID</param>
    /// <param name="size">The size of the avatar (default: 80)</param>
    /// <returns>The file path of the generated avatar</returns>
    Task<string> GenerateSpaceAvatarAsync(string spaceId, int size = 80);

    /// <summary>
    /// Generates an avatar SVG file for a community and saves it to disk.
    /// </summary>
    /// <param name="communityId">The community's public ID</param>
    /// <param name="size">The size of the avatar (default: 80)</param>
    /// <returns>The file path of the generated avatar</returns>
    Task<string> GenerateCommunityAvatarAsync(string communityId, int size = 80);

    /// <summary>
    /// Checks if an avatar file exists for the given entity.
    /// </summary>
    /// <param name="entityType">Type of entity (user, hub, space, community)</param>
    /// <param name="entityId">The entity's public ID</param>
    /// <returns>True if the avatar file exists, false otherwise</returns>
    Task<bool> AvatarExistsAsync(string entityType, string entityId);

    /// <summary>
    /// Deletes an avatar file from disk.
    /// </summary>
    /// <param name="entityType">Type of entity (user, hub, space, community)</param>
    /// <param name="entityId">The entity's public ID</param>
    Task DeleteAvatarAsync(string entityType, string entityId);

    /// <summary>
    /// Generates all missing avatars for existing entities in the database.
    /// This is typically run on application startup.
    /// </summary>
    /// <returns>The total count of avatars generated</returns>
    Task<int> GenerateAllMissingAvatarsAsync();
}
