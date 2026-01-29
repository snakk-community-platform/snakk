namespace Snakk.Domain.ValueObjects;

public enum UserRoleType
{
    GlobalAdmin,      // Platform-wide admin (Snakk staff)
    CommunityAdmin,   // Community owner/admin - can configure community + all mod powers
    CommunityMod,     // Community-level moderator
    HubMod,           // Hub-level moderator
    SpaceMod          // Space-level moderator
}
