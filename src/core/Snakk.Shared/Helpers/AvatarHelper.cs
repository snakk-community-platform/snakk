using System.IO.Hashing;
using System.Text;

namespace Snakk.Shared.Helpers;

public enum AvatarEntityType
{
    User,
    Community,
    Hub,
    Space
}

public static class AvatarHelper
{
    /// <summary>
    /// Gets the relative file path for an avatar with optional revision.
    /// Uses XxHash32 to distribute across 256 shard folders.
    /// </summary>
    /// <param name="publicId">Entity's public ID (ULID)</param>
    /// <param name="entityType">Type of entity (User, Community, Hub, Space)</param>
    /// <param name="revision">Avatar revision number (0 = default)</param>
    /// <returns>Relative path like "4a/userId.svg" or "4a/userId-r5.svg"</returns>
    public static string GetAvatarPath(string publicId, AvatarEntityType entityType, int revision = 0)
    {
        Span<byte> utf8 = stackalloc byte[64];
        var len = Encoding.UTF8.GetBytes(publicId, utf8);

        var hash = XxHash32.HashToUInt32(utf8.Slice(0, len));
        var shard = (byte)hash;

        var fileName = revision == 0
            ? $"{publicId}.svg"
            : $"{publicId}-r{revision}.svg";

        return $"{shard:x2}/{fileName}";
    }

    /// <summary>
    /// Gets the full relative path including entity folder for use with IFileStorage.
    /// Example: "avatars/generated/users/4a/userId.svg"
    /// </summary>
    public static string GetFullRelativePath(string publicId, AvatarEntityType entityType, int revision = 0)
    {
        var avatarPath = GetAvatarPath(publicId, entityType, revision);
        var entityFolder = GetEntityFolder(entityType);

        return $"avatars/generated/{entityFolder}/{avatarPath}";
    }

    /// <summary>
    /// Gets the public URL for an avatar.
    /// Example: "/avatars/generated/users/4a/userId.svg"
    /// </summary>
    public static string GetAvatarUrl(string publicId, AvatarEntityType entityType, int revision = 0)
    {
        var avatarPath = GetAvatarPath(publicId, entityType, revision);
        var entityFolder = GetEntityFolder(entityType);

        return $"/avatars/generated/{entityFolder}/{avatarPath}";
    }

    /// <summary>
    /// Gets the shard folder name (e.g., "4a") for a given public ID.
    /// </summary>
    public static string GetShardFolder(string publicId)
    {
        Span<byte> utf8 = stackalloc byte[64];
        var len = Encoding.UTF8.GetBytes(publicId, utf8);

        var hash = XxHash32.HashToUInt32(utf8.Slice(0, len));
        var shard = (byte)hash;

        return $"{shard:x2}";
    }

    /// <summary>
    /// Gets the entity folder name for a given entity type.
    /// </summary>
    public static string GetEntityFolder(AvatarEntityType entityType) => entityType switch
    {
        AvatarEntityType.User => "users",
        AvatarEntityType.Community => "communities",
        AvatarEntityType.Hub => "hubs",
        AvatarEntityType.Space => "spaces",
        _ => throw new ArgumentException($"Unknown entity type: {entityType}")
    };
}
