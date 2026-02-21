"use strict";
/**
 * Read History Manager
 * Tracks discussion visits in browser localStorage
 */
// ============================================================================
// Implementation
// ============================================================================
(function () {
    'use strict';
    const STORAGE_KEY = 'snakk_read_history';
    const MAX_HISTORY_ITEMS = 50;
    function getReadHistory() {
        try {
            const stored = localStorage.getItem(STORAGE_KEY);
            return stored ? JSON.parse(stored) : [];
        }
        catch (e) {
            console.error('Failed to load read history:', e);
            return [];
        }
    }
    function saveReadHistory(history) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(history));
        }
        catch (e) {
            console.error('Failed to save read history:', e);
        }
    }
    function addToReadHistory(discussion) {
        if (!discussion || !discussion.discussionPublicId) {
            console.error('Invalid discussion data for read history');
            return;
        }
        let history = getReadHistory();
        // Remove existing entry if present
        history = history.filter(item => item.discussionPublicId !== discussion.discussionPublicId);
        // Create new history entry
        const entry = {
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
    function clearReadHistory() {
        try {
            localStorage.removeItem(STORAGE_KEY);
        }
        catch (e) {
            console.error('Failed to clear read history:', e);
        }
    }
    function removeFromReadHistory(discussionPublicId) {
        let history = getReadHistory();
        history = history.filter(item => item.discussionPublicId !== discussionPublicId);
        saveReadHistory(history);
    }
    function buildDiscussionUrl(entry) {
        const communityPrefix = entry.isDefaultCommunity ? '' : `/c/${entry.communitySlug}`;
        return `${communityPrefix}/h/${entry.hubSlug}/${entry.spaceSlug}/${entry.discussionSlug}~${entry.discussionPublicId}`;
    }
    // Export API
    window.SnakkReadHistory = {
        getHistory: getReadHistory,
        addToHistory: addToReadHistory,
        clearHistory: clearReadHistory,
        removeFromHistory: removeFromReadHistory,
        buildUrl: buildDiscussionUrl
    };
})();
//# sourceMappingURL=read-history.js.map