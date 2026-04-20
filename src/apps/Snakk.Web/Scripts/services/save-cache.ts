/**
 * Save/Bookmark Cache Manager
 * Caches saved discussion and post IDs to reduce API calls
 * TTL: 5 minutes, invalidates on mutations
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface SaveIdsResponse {
    publicIds: string[];
}

interface SnakkSaveCacheAPI {
    isDiscussionSaved(discussionId: string): boolean | null;
    isPostSaved(postId: string): boolean | null;
    setDiscussionSaved(discussionId: string, isSaved: boolean): void;
    setPostSaved(postId: string, isSaved: boolean): void;
    syncSavedDiscussions(): Promise<string[]>;
    syncSavedPosts(): Promise<string[]>;
    getSavedDiscussions(): string[];
    getSavedPosts(): string[];
    getLastSync(): number | null;
    isSyncStale(): boolean;
    updateLastSync(): void;
    invalidateDiscussion(discussionId: string): void;
    invalidatePost(postId: string): void;
    clearAllCaches(): void;
}


// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    // Initialize cache managers (5 min TTL)
    const CacheManager = (window as any).CacheManager;
    const savedDiscussionsCache = new CacheManager('snakk_saved_discussions', 5, 200);
    const savedPostsCache = new CacheManager('snakk_saved_posts', 5, 200);

    // Track last full sync timestamp
    const SYNC_KEY = 'snakk_save_last_sync';

    function isDiscussionSaved(discussionId: string): boolean | null {
        const cached = savedDiscussionsCache.get(discussionId);
        return cached !== null ? cached : null;
    }

    function isPostSaved(postId: string): boolean | null {
        const cached = savedPostsCache.get(postId);
        return cached !== null ? cached : null;
    }

    function setDiscussionSaved(discussionId: string, isSaved: boolean): void {
        savedDiscussionsCache.set(discussionId, isSaved);
    }

    function setPostSaved(postId: string, isSaved: boolean): void {
        savedPostsCache.set(postId, isSaved);
    }

    async function syncSavedDiscussions(): Promise<string[]> {
        try {
            const response = await fetch('/bff/saves/discussion-ids', { credentials: 'include' });
            if (!response.ok) return [];

            const data = await response.json() as SaveIdsResponse;
            const discussionIds = data.publicIds || [];

            savedDiscussionsCache.clear();
            discussionIds.forEach((id: string) => {
                savedDiscussionsCache.set(id, true);
            });

            updateLastSync();
            return discussionIds;
        } catch (err) {
            console.error('Failed to sync saved discussions:', err);
            return [];
        }
    }

    async function syncSavedPosts(): Promise<string[]> {
        try {
            const response = await fetch('/bff/saves/post-ids', { credentials: 'include' });
            if (!response.ok) return [];

            const data = await response.json() as SaveIdsResponse;
            const postIds = data.publicIds || [];

            savedPostsCache.clear();
            postIds.forEach((id: string) => {
                savedPostsCache.set(id, true);
            });

            updateLastSync();
            return postIds;
        } catch (err) {
            console.error('Failed to sync saved posts:', err);
            return [];
        }
    }

    function getSavedDiscussions(): string[] {
        const cache = savedDiscussionsCache.getAllValid();
        return Object.keys(cache).filter((id: string) => cache[id] === true);
    }

    function getSavedPosts(): string[] {
        const cache = savedPostsCache.getAllValid();
        return Object.keys(cache).filter((id: string) => cache[id] === true);
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
        savedDiscussionsCache.clear();
        savedPostsCache.clear();
        localStorage.removeItem(SYNC_KEY);
    }

    function invalidateDiscussion(discussionId: string): void {
        savedDiscussionsCache.remove(discussionId);
    }

    function invalidatePost(postId: string): void {
        savedPostsCache.remove(postId);
    }

    // Export API
    (window as any).SnakkSaveCache = {
        isDiscussionSaved,
        isPostSaved,
        setDiscussionSaved,
        setPostSaved,
        syncSavedDiscussions,
        syncSavedPosts,
        getSavedDiscussions,
        getSavedPosts,
        getLastSync,
        isSyncStale,
        updateLastSync,
        invalidateDiscussion,
        invalidatePost,
        clearAllCaches
    };
})();
