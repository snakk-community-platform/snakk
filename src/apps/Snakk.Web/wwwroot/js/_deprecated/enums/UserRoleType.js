/**
 * User Role Types (mirrors Snakk.Shared.Enums.UserRoleTypeEnum)
 * These role names must match the C# enum string values exactly
 */
(function() {
    'use strict';

    window.UserRoleType = Object.freeze({
        // Role constants
        GlobalAdmin: 'GlobalAdmin',      // Platform-wide admin (Snakk staff)
        CommunityAdmin: 'CommunityAdmin', // Community owner/admin
        CommunityMod: 'CommunityMod',     // Community-level moderator
        HubMod: 'HubMod',                 // Hub-level moderator
        SpaceMod: 'SpaceMod',             // Space-level moderator

        /**
         * Check if a role has global admin privileges
         */
        isGlobalAdmin: function(role) {
            return role === 'GlobalAdmin';
        },

        /**
         * Check if a role has any moderation privileges
         */
        hasModeratorPrivileges: function(role) {
            return role && ['GlobalAdmin', 'CommunityAdmin', 'CommunityMod', 'HubMod', 'SpaceMod'].includes(role);
        },

        /**
         * Check if a role has admin privileges at any level
         */
        hasAdminPrivileges: function(role) {
            return role === 'GlobalAdmin' || role === 'CommunityAdmin';
        }
    });
})();
