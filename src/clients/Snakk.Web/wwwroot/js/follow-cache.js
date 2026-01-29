/**
 * Follow Status Cache Manager
 * Caches followed spaces, discussions, and users to reduce API calls
 * TTL: 5 minutes, invalidates on mutations
 */
(function() {
    'use strict';

    // Initialize cache managers (5 min TTL)
    const followedSpacesCache = new CacheManager('snakk_followed_spaces', 5, 100);
    const followedDiscussionsCache = new CacheManager('snakk_followed_discussions', 5, 100);
    const followedUsersCache = new CacheManager('snakk_followed_users', 5, 100);

    // Track last full sync timestamp
    const SYNC_KEY = 'snakk_follow_last_sync';

    /**
     * Check if a space is followed (cached)
     * @param {string} spaceId
     * @returns {boolean|null} true/false if cached, null if not in cache
     */
    function isSpaceFollowed(spaceId) {
        const cached = followedSpacesCache.get(spaceId);
        return cached !== null ? cached : null;
    }

    /**
     * Check if a discussion is followed (cached)
     * @param {string} discussionId
     * @returns {boolean|null}
     */
    function isDiscussionFollowed(discussionId) {
        const cached = followedDiscussionsCache.get(discussionId);
        return cached !== null ? cached : null;
    }

    /**
     * Check if a user is followed (cached)
     * @param {string} userId
     * @returns {boolean|null}
     */
    function isUserFollowed(userId) {
        const cached = followedUsersCache.get(userId);
        return cached !== null ? cached : null;
    }

    /**
     * Set space follow status and update cache
     * @param {string} spaceId
     * @param {boolean} isFollowing
     */
    function setSpaceFollowed(spaceId, isFollowing) {
        followedSpacesCache.set(spaceId, isFollowing);
    }

    /**
     * Set discussion follow status and update cache
     * @param {string} discussionId
     * @param {boolean} isFollowing
     */
    function setDiscussionFollowed(discussionId, isFollowing) {
        followedDiscussionsCache.set(discussionId, isFollowing);
    }

    /**
     * Set user follow status and update cache
     * @param {string} userId
     * @param {boolean} isFollowing
     */
    function setUserFollowed(userId, isFollowing) {
        followedUsersCache.set(userId, isFollowing);
    }

    /**
     * Fetch and cache all followed spaces from API
     * @returns {Promise<Array>}
     */
    async function syncFollowedSpaces() {
        try {
            const response = await fetch('/bff/follows/spaces', { credentials: 'include' });
            if (!response.ok) return [];

            const data = await response.json();
            const spaceIds = data.items || [];

            // Clear old cache and rebuild
            followedSpacesCache.clear();
            spaceIds.forEach(spaceId => {
                followedSpacesCache.set(spaceId, true);
            });

            updateLastSync();
            return spaceIds;
        } catch (err) {
            console.error('Failed to sync followed spaces:', err);
            return [];
        }
    }

    /**
     * Fetch and cache all followed discussions from API
     * @returns {Promise<Array>}
     */
    async function syncFollowedDiscussions() {
        try {
            const response = await fetch('/bff/follows/discussions', { credentials: 'include' });
            if (!response.ok) return [];

            const data = await response.json();
            const discussionIds = data.items || [];

            followedDiscussionsCache.clear();
            discussionIds.forEach(discussionId => {
                followedDiscussionsCache.set(discussionId, true);
            });

            updateLastSync();
            return discussionIds;
        } catch (err) {
            console.error('Failed to sync followed discussions:', err);
            return [];
        }
    }

    /**
     * Fetch and cache all followed users from API
     * @returns {Promise<Array>}
     */
    async function syncFollowedUsers() {
        try {
            const response = await fetch('/bff/follows/users', { credentials: 'include' });
            if (!response.ok) return [];

            const data = await response.json();
            const userIds = data.items || [];

            followedUsersCache.clear();
            userIds.forEach(userId => {
                followedUsersCache.set(userId, true);
            });

            updateLastSync();
            return userIds;
        } catch (err) {
            console.error('Failed to sync followed users:', err);
            return [];
        }
    }

    /**
     * Get all followed space IDs from cache
     * @returns {Array<string>}
     */
    function getFollowedSpaces() {
        const cache = followedSpacesCache.getAllValid();
        return Object.keys(cache).filter(id => cache[id] === true);
    }

    /**
     * Get all followed discussion IDs from cache
     * @returns {Array<string>}
     */
    function getFollowedDiscussions() {
        const cache = followedDiscussionsCache.getAllValid();
        return Object.keys(cache).filter(id => cache[id] === true);
    }

    /**
     * Get all followed user IDs from cache
     * @returns {Array<string>}
     */
    function getFollowedUsers() {
        const cache = followedUsersCache.getAllValid();
        return Object.keys(cache).filter(id => cache[id] === true);
    }

    /**
     * Update last sync timestamp
     */
    function updateLastSync() {
        localStorage.setItem(SYNC_KEY, Date.now().toString());
    }

    /**
     * Get last sync timestamp
     * @returns {number|null}
     */
    function getLastSync() {
        const stored = localStorage.getItem(SYNC_KEY);
        return stored ? parseInt(stored, 10) : null;
    }

    /**
     * Check if sync is stale (older than 5 minutes)
     * @returns {boolean}
     */
    function isSyncStale() {
        const lastSync = getLastSync();
        if (!lastSync) return true;
        return Date.now() - lastSync > 5 * 60 * 1000;
    }

    /**
     * Clear all follow caches
     */
    function clearAllCaches() {
        followedSpacesCache.clear();
        followedDiscussionsCache.clear();
        followedUsersCache.clear();
        localStorage.removeItem(SYNC_KEY);
    }

    /**
     * Invalidate a specific space's follow status (force refresh on next check)
     * @param {string} spaceId
     */
    function invalidateSpace(spaceId) {
        followedSpacesCache.remove(spaceId);
    }

    /**
     * Invalidate a specific discussion's follow status
     * @param {string} discussionId
     */
    function invalidateDiscussion(discussionId) {
        followedDiscussionsCache.remove(discussionId);
    }

    /**
     * Invalidate a specific user's follow status
     * @param {string} userId
     */
    function invalidateUser(userId) {
        followedUsersCache.remove(userId);
    }

    // Export API
    window.SnakkFollowCache = {
        // Check status (returns cached value or null)
        isSpaceFollowed,
        isDiscussionFollowed,
        isUserFollowed,

        // Update status (after API mutation)
        setSpaceFollowed,
        setDiscussionFollowed,
        setUserFollowed,

        // Sync from API
        syncFollowedSpaces,
        syncFollowedDiscussions,
        syncFollowedUsers,

        // Get all followed IDs
        getFollowedSpaces,
        getFollowedDiscussions,
        getFollowedUsers,

        // Sync metadata
        getLastSync,
        isSyncStale,
        updateLastSync,

        // Invalidation
        invalidateSpace,
        invalidateDiscussion,
        invalidateUser,
        clearAllCaches
    };
})();
