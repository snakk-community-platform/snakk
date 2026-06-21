namespace Snakk.Application.DTOs.Management;

// Basic scope entity returned from scope resolution
public class ScopeEntityDto
{
    public int DbId { get; init; }
    public required string PublicId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
}

// Extended scope entity with avatar filename (for gRPC scope resolution)
public class ScopeEntityWithAvatarDto : ScopeEntityDto
{
    public string? AvatarFileName { get; init; }
}

// Discord settings for a space
public class SpaceDiscordSettingsDto
{
    public string? DiscordWebhookUrl { get; init; }
    public string? DiscordChannelName { get; init; }
    public string? DiscordInviteUrl { get; init; }
}

// Parent info for hub (for inherited ban/team/report lookups)
public class HubParentDto
{
    public required string CommunityPublicId { get; init; }
    public required string CommunityName { get; init; }
}

// Parent info for space (for inherited ban/team/report lookups)
public class SpaceParentsDto
{
    public required string CommunityPublicId { get; init; }
    public required string CommunityName { get; init; }
    public required string HubPublicId { get; init; }
    public required string HubName { get; init; }
}

// Hub with community info (for report-reasons parent lookup using Include navigation)
public class HubWithCommunityDto
{
    public required string HubPublicId { get; init; }
    public required string HubName { get; init; }
    public required string CommunityPublicId { get; init; }
    public required string CommunityName { get; init; }
}

// Space with hub+community info (for report-reasons parent lookup)
public class SpaceWithHubCommunityDto
{
    public required string SpacePublicId { get; init; }
    public required string SpaceName { get; init; }
    public required string HubPublicId { get; init; }
    public required string HubName { get; init; }
    public required string CommunityPublicId { get; init; }
    public required string CommunityName { get; init; }
}

// Seed data for building GetModerators response for a Space
public class SpaceModeratorSeedDto
{
    public int SpaceDbId { get; init; }
    public required string SpaceName { get; init; }
    public int HubDbId { get; init; }
    public required string HubName { get; init; }
    public int CommunityDbId { get; init; }
    public required string CommunityName { get; init; }
}

// Seed data for building GetModerators response for a Hub
public class HubModeratorSeedDto
{
    public int HubDbId { get; init; }
    public required string HubName { get; init; }
    public int CommunityDbId { get; init; }
    public required string CommunityName { get; init; }
}

// Seed data for building GetModerators response for a Community
public class CommunityModeratorSeedDto
{
    public int CommunityDbId { get; init; }
    public required string CommunityName { get; init; }
}

// Single moderator/admin entry returned from role queries
public class ModeratorInfoDto
{
    public required string UserPublicId { get; init; }
    public required string DisplayName { get; init; }
    public required string Role { get; init; }
    public required string Slug { get; init; }
}
