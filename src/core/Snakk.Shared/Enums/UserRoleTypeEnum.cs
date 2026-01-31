using System.Text.Json.Serialization;

namespace Snakk.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserRoleTypeEnum
{
    GlobalAdmin = 1,      // Platform-wide admin (Snakk staff)
    CommunityAdmin = 2,   // Community owner/admin
    CommunityMod = 3,     // Community-level moderator
    HubMod = 4,           // Hub-level moderator
    SpaceMod = 5          // Space-level moderator
}
