/**
 * Read History Manager
 * Tracks discussion visits in browser localStorage
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface ReadHistoryEntry {
    discussionPublicId: string;
    discussionTitle: string;
    discussionSlug: string;
    spacePublicId: string;
    spaceSlug: string;
    spaceName: string;
    hubPublicId: string;
    hubSlug: string;
    hubName: string;
    communityPublicId: string;
    communitySlug: string;
    communityName: string;
    isDefaultCommunity: boolean;
    lastActivityAt: string;
    visitedAt: string;
}

interface SnakkReadHistoryAPI {
    getHistory(): ReadHistoryEntry[];
    addToHistory(discussion: Partial<ReadHistoryEntry>): void;
    clearHistory(): void;
    removeFromHistory(discussionPublicId: string): void;
    buildUrl(entry: ReadHistoryEntry): string;
}


// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    const STORAGE_KEY = 'snakk_read_history';
    const MAX_HISTORY_ITEMS = 50;

    function getReadHistory(): ReadHistoryEntry[] {
        try {
            const stored = localStorage.getItem(STORAGE_KEY);
            return stored ? JSON.parse(stored) as ReadHistoryEntry[] : [];
        } catch (e) {
            console.error('Failed to load read history:', e);
            return [];
        }
    }

    function saveReadHistory(history: ReadHistoryEntry[]): void {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(history));
        } catch (e) {
            console.error('Failed to save read history:', e);
        }
    }

    function addToReadHistory(discussion: Partial<ReadHistoryEntry>): void {
        if (!discussion || !discussion.discussionPublicId) {
            console.error('Invalid discussion data for read history');
            return;
        }

        let history = getReadHistory();

        // Remove existing entry if present
        history = history.filter(item => item.discussionPublicId !== discussion.discussionPublicId);

        // Create new history entry
        const entry: ReadHistoryEntry = {
            discussionPublicId: discussion.discussionPublicId,
            discussionTitle: discussion.discussionTitle || '',
            discussionSlug: discussion.discussionSlug || '',
            spacePublicId: discussion.spacePublicId || '',
            spaceSlug: discussion.spaceSlug || '',
            spaceName: discussion.spaceName || '',
            hubPublicId: discussion.hubPublicId || '',
            hubSlug: discussion.hubSlug || '',
            hubName: discussion.hubName || '',
            communityPublicId: discussion.communityPublicId || '',
            communitySlug: discussion.communitySlug || '',
            communityName: discussion.communityName || '',
            isDefaultCommunity: discussion.isDefaultCommunity || false,
            lastActivityAt: discussion.lastActivityAt || '',
            visitedAt: new Date().toISOString()
        };

        history.unshift(entry);

        // Limit history size
        if (history.length > MAX_HISTORY_ITEMS) {
            history = history.slice(0, MAX_HISTORY_ITEMS);
        }

        saveReadHistory(history);
    }

    function clearReadHistory(): void {
        try {
            localStorage.removeItem(STORAGE_KEY);
        } catch (e) {
            console.error('Failed to clear read history:', e);
        }
    }

    function removeFromReadHistory(discussionPublicId: string): void {
        let history = getReadHistory();
        history = history.filter(item => item.discussionPublicId !== discussionPublicId);
        saveReadHistory(history);
    }

    function buildDiscussionUrl(entry: ReadHistoryEntry): string {
        const communityPrefix = entry.isDefaultCommunity ? '' : `/c/${entry.communitySlug}`;
        return `${communityPrefix}/h/${entry.hubSlug}/${entry.spaceSlug}/${entry.discussionSlug}~${entry.discussionPublicId}`;
    }

    // Export API
    (window as any).SnakkReadHistory = {
        getHistory: getReadHistory,
        addToHistory: addToReadHistory,
        clearHistory: clearReadHistory,
        removeFromHistory: removeFromReadHistory,
        buildUrl: buildDiscussionUrl
    };
})();
