/**
 * User Role Types (mirrors Snakk.Shared.Enums.UserRoleTypeEnum)
 * These role names must match the C# enum string values exactly
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface UserRoleTypeEnum {
    // Role constants (string values)
    readonly GlobalAdmin: 'GlobalAdmin';
    readonly CommunityAdmin: 'CommunityAdmin';
    readonly CommunityMod: 'CommunityMod';
    readonly HubMod: 'HubMod';
    readonly SpaceMod: 'SpaceMod';

    // Helper methods
    isGlobalAdmin(role: string | null | undefined): boolean;
    hasModeratorPrivileges(role: string | null | undefined): boolean;
    hasAdminPrivileges(role: string | null | undefined): boolean;
}


// ============================================================================
// Implementation
// ============================================================================

const UserRoleType: UserRoleTypeEnum = Object.freeze({
    // Role constants
    GlobalAdmin: 'GlobalAdmin',      // Platform-wide admin (Snakk staff)
    CommunityAdmin: 'CommunityAdmin', // Community owner/admin
    CommunityMod: 'CommunityMod',     // Community-level moderator
    HubMod: 'HubMod',                 // Hub-level moderator
    SpaceMod: 'SpaceMod',             // Space-level moderator

    /**
     * Check if a role has global admin privileges
     */
    isGlobalAdmin(role: string | null | undefined): boolean {
        return role === 'GlobalAdmin';
    },

    /**
     * Check if a role has any moderation privileges
     */
    hasModeratorPrivileges(role: string | null | undefined): boolean {
        return role != null && ['GlobalAdmin', 'CommunityAdmin', 'CommunityMod', 'HubMod', 'SpaceMod'].includes(role);
    },

    /**
     * Check if a role has admin privileges at any level
     */
    hasAdminPrivileges(role: string | null | undefined): boolean {
        return role === 'GlobalAdmin' || role === 'CommunityAdmin';
    }
});

// Export to window for backward compatibility
(window as any).UserRoleType = UserRoleType;
