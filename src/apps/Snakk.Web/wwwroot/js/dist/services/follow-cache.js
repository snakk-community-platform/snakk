"use strict";
/**
 * Follow Status Cache Manager
 * Caches followed spaces, discussions, and users to reduce API calls
 * TTL: 5 minutes, invalidates on mutations
 */
// ============================================================================
// Implementation
// ============================================================================
(function () {
    'use strict';
    // Initialize cache managers (5 min TTL)
    const CacheManager = window.CacheManager;
    const followedSpacesCache = new CacheManager('snakk_followed_spaces', 5, 100);
    const followedDiscussionsCache = new CacheManager('snakk_followed_discussions', 5, 100);
    const followedUsersCache = new CacheManager('snakk_followed_users', 5, 100);
    // Track last full sync timestamp
    const SYNC_KEY = 'snakk_follow_last_sync';
    function isSpaceFollowed(spaceId) {
        const cached = followedSpacesCache.get(spaceId);
        return cached !== null ? cached : null;
    }
    function isDiscussionFollowed(discussionId) {
        const cached = followedDiscussionsCache.get(discussionId);
        return cached !== null ? cached : null;
    }
    function isUserFollowed(userId) {
        const cached = followedUsersCache.get(userId);
        return cached !== null ? cached : null;
    }
    function setSpaceFollowed(spaceId, isFollowing) {
        followedSpacesCache.set(spaceId, isFollowing);
    }
    function setDiscussionFollowed(discussionId, isFollowing) {
        followedDiscussionsCache.set(discussionId, isFollowing);
    }
    function setUserFollowed(userId, isFollowing) {
        followedUsersCache.set(userId, isFollowing);
    }
    async function syncFollowedSpaces() {
        try {
            const response = await fetch('/bff/follows/spaces', { credentials: 'include' });
            if (!response.ok)
                return [];
            const data = await response.json();
            const spaceIds = data.items || [];
            followedSpacesCache.clear();
            spaceIds.forEach(spaceId => {
                followedSpacesCache.set(spaceId, true);
            });
            updateLastSync();
            return spaceIds;
        }
        catch (err) {
            console.error('Failed to sync followed spaces:', err);
            return [];
        }
    }
    async function syncFollowedDiscussions() {
        try {
            const response = await fetch('/bff/follows/discussions', { credentials: 'include' });
            if (!response.ok)
                return [];
            const data = await response.json();
            const discussionIds = data.items || [];
            followedDiscussionsCache.clear();
            discussionIds.forEach(discussionId => {
                followedDiscussionsCache.set(discussionId, true);
            });
            updateLastSync();
            return discussionIds;
        }
        catch (err) {
            console.error('Failed to sync followed discussions:', err);
            return [];
        }
    }
    async function syncFollowedUsers() {
        try {
            const response = await fetch('/bff/follows/users', { credentials: 'include' });
            if (!response.ok)
                return [];
            const data = await response.json();
            const userIds = data.items || [];
            followedUsersCache.clear();
            userIds.forEach(userId => {
                followedUsersCache.set(userId, true);
            });
            updateLastSync();
            return userIds;
        }
        catch (err) {
            console.error('Failed to sync followed users:', err);
            return [];
        }
    }
    function getFollowedSpaces() {
        const cache = followedSpacesCache.getAllValid();
        return Object.keys(cache).filter(id => cache[id] === true);
    }
    function getFollowedDiscussions() {
        const cache = followedDiscussionsCache.getAllValid();
        return Object.keys(cache).filter(id => cache[id] === true);
    }
    function getFollowedUsers() {
        const cache = followedUsersCache.getAllValid();
        return Object.keys(cache).filter(id => cache[id] === true);
    }
    function updateLastSync() {
        localStorage.setItem(SYNC_KEY, Date.now().toString());
    }
    function getLastSync() {
        const stored = localStorage.getItem(SYNC_KEY);
        return stored ? parseInt(stored, 10) : null;
    }
    function isSyncStale() {
        const lastSync = getLastSync();
        if (!lastSync)
            return true;
        return Date.now() - lastSync > 5 * 60 * 1000;
    }
    function clearAllCaches() {
        followedSpacesCache.clear();
        followedDiscussionsCache.clear();
        followedUsersCache.clear();
        localStorage.removeItem(SYNC_KEY);
    }
    function invalidateSpace(spaceId) {
        followedSpacesCache.remove(spaceId);
    }
    function invalidateDiscussion(discussionId) {
        followedDiscussionsCache.remove(discussionId);
    }
    function invalidateUser(userId) {
        followedUsersCache.remove(userId);
    }
    // Export API
    window.SnakkFollowCache = {
        isSpaceFollowed,
        isDiscussionFollowed,
        isUserFollowed,
        setSpaceFollowed,
        setDiscussionFollowed,
        setUserFollowed,
        syncFollowedSpaces,
        syncFollowedDiscussions,
        syncFollowedUsers,
        getFollowedSpaces,
        getFollowedDiscussions,
        getFollowedUsers,
        getLastSync,
        isSyncStale,
        updateLastSync,
        invalidateSpace,
        invalidateDiscussion,
        invalidateUser,
        clearAllCaches
    };
})();
//# sourceMappingURL=follow-cache.js.map