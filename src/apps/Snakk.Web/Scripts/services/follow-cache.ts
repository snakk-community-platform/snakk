/**
 * Follow Status Cache Manager
 * Caches followed spaces, discussions, and users to reduce API calls
 * TTL: 5 minutes, invalidates on mutations
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface FollowResponse {
    items: string[];
}

interface SnakkFollowCacheAPI {
    isSpaceFollowed(spaceId: string): boolean | null;
    isDiscussionFollowed(discussionId: string): boolean | null;
    isUserFollowed(userId: string): boolean | null;
    setSpaceFollowed(spaceId: string, isFollowing: boolean): void;
    setDiscussionFollowed(discussionId: string, isFollowing: boolean): void;
    setUserFollowed(userId: string, isFollowing: boolean): void;
    syncFollowedSpaces(): Promise<string[]>;
    syncFollowedDiscussions(): Promise<string[]>;
    syncFollowedUsers(): Promise<string[]>;
    getFollowedSpaces(): string[];
    getFollowedDiscussions(): string[];
    getFollowedUsers(): string[];
    getLastSync(): number | null;
    isSyncStale(): boolean;
    updateLastSync(): void;
    invalidateSpace(spaceId: string): void;
    invalidateDiscussion(discussionId: string): void;
    invalidateUser(userId: string): void;
    clearAllCaches(): void;
}


// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    // Initialize cache managers (5 min TTL)
    const CacheManager = (window as any).CacheManager;
    const followedSpacesCache = new CacheManager('snakk_followed_spaces', 5, 100);
    const followedDiscussionsCache = new CacheManager('snakk_followed_discussions', 5, 100);
    const followedUsersCache = new CacheManager('snakk_followed_users', 5, 100);

    // Track last full sync timestamp
    const SYNC_KEY = 'snakk_follow_last_sync';

    function isSpaceFollowed(spaceId: string): boolean | null {
        const cached = followedSpacesCache.get(spaceId);
        return cached !== null ? cached : null;
    }

    function isDiscussionFollowed(discussionId: string): boolean | null {
        const cached = followedDiscussionsCache.get(discussionId);
        return cached !== null ? cached : null;
    }

    function isUserFollowed(userId: string): boolean | null {
        const cached = followedUsersCache.get(userId);
        return cached !== null ? cached : null;
    }

    function setSpaceFollowed(spaceId: string, isFollowing: boolean): void {
        followedSpacesCache.set(spaceId, isFollowing);
    }

    function setDiscussionFollowed(discussionId: string, isFollowing: boolean): void {
        followedDiscussionsCache.set(discussionId, isFollowing);
    }

    function setUserFollowed(userId: string, isFollowing: boolean): void {
        followedUsersCache.set(userId, isFollowing);
    }

    async function syncFollowedSpaces(): Promise<string[]> {
        try {
            const response = await fetch('/bff/follows/spaces', { credentials: 'include' });
            if (!response.ok) return [];

            const data = await response.json() as FollowResponse;
            const spaceIds = data.items || [];

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

    async function syncFollowedDiscussions(): Promise<string[]> {
        try {
            const response = await fetch('/bff/follows/discussions', { credentials: 'include' });
            if (!response.ok) return [];

            const data = await response.json() as FollowResponse;
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

    async function syncFollowedUsers(): Promise<string[]> {
        try {
            const response = await fetch('/bff/follows/users', { credentials: 'include' });
            if (!response.ok) return [];

            const data = await response.json() as FollowResponse;
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

    function getFollowedSpaces(): string[] {
        const cache = followedSpacesCache.getAllValid();
        return Object.keys(cache).filter(id => cache[id] === true);
    }

    function getFollowedDiscussions(): string[] {
        const cache = followedDiscussionsCache.getAllValid();
        return Object.keys(cache).filter(id => cache[id] === true);
    }

    function getFollowedUsers(): string[] {
        const cache = followedUsersCache.getAllValid();
        return Object.keys(cache).filter(id => cache[id] === true);
    }

    function updateLastSync(): void {
        localStorage.setItem(SYNC_KEY, Date.now().toString());
    }

    function getLastSync(): number | null {
        const stored = localStorage.getItem(SYNC_KEY);
        return stored ? parseInt(stored, 10) : null;
    }

    function isSyncStale(): boolean {
        const lastSync = getLastSync();
        if (!lastSync) return true;
        return Date.now() - lastSync > 5 * 60 * 1000;
    }

    function clearAllCaches(): void {
        followedSpacesCache.clear();
        followedDiscussionsCache.clear();
        followedUsersCache.clear();
        localStorage.removeItem(SYNC_KEY);
    }

    function invalidateSpace(spaceId: string): void {
        followedSpacesCache.remove(spaceId);
    }

    function invalidateDiscussion(discussionId: string): void {
        followedDiscussionsCache.remove(discussionId);
    }

    function invalidateUser(userId: string): void {
        followedUsersCache.remove(userId);
    }

    // Export API
    (window as any).SnakkFollowCache = {
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
