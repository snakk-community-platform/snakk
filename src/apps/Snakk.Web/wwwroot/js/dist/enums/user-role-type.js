"use strict";
/**
 * User Role Types (mirrors Snakk.Shared.Enums.UserRoleTypeEnum)
 * These role names must match the C# enum string values exactly
 */
// ============================================================================
// Implementation
// ============================================================================
const UserRoleType = Object.freeze({
    // Role constants
    GlobalAdmin: 'GlobalAdmin', // Platform-wide admin (Snakk staff)
    CommunityAdmin: 'CommunityAdmin', // Community owner/admin
    CommunityMod: 'CommunityMod', // Community-level moderator
    HubMod: 'HubMod', // Hub-level moderator
    SpaceMod: 'SpaceMod', // Space-level moderator
    /**
     * Check if a role has global admin privileges
     */
    isGlobalAdmin(role) {
        return role === 'GlobalAdmin';
    },
    /**
     * Check if a role has any moderation privileges
     */
    hasModeratorPrivileges(role) {
        return role != null && ['GlobalAdmin', 'CommunityAdmin', 'CommunityMod', 'HubMod', 'SpaceMod'].includes(role);
    },
    /**
     * Check if a role has admin privileges at any level
     */
    hasAdminPrivileges(role) {
        return role === 'GlobalAdmin' || role === 'CommunityAdmin';
    }
});
// Export to window for backward compatibility
window.UserRoleType = UserRoleType;
//# sourceMappingURL=user-role-type.js.map