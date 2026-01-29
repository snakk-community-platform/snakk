/**
 * Read History Manager
 * Tracks discussion visits in browser localStorage
 */
(function() {
    'use strict';

    const STORAGE_KEY = 'snakk_read_history';
    const MAX_HISTORY_ITEMS = 50;

    /**
     * Get all read history entries
     * @returns {Array} Array of read history items, most recent first
     */
    function getReadHistory() {
        try {
            const stored = localStorage.getItem(STORAGE_KEY);
            return stored ? JSON.parse(stored) : [];
        } catch (e) {
            console.error('Failed to load read history:', e);
            return [];
        }
    }

    /**
     * Save read history to localStorage
     * @param {Array} history - Array of history items
     */
    function saveReadHistory(history) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(history));
        } catch (e) {
            console.error('Failed to save read history:', e);
        }
    }

    /**
     * Add a discussion visit to read history
     * If already exists, moves it to the top
     * @param {Object} discussion - Discussion data
     * @param {string} discussion.discussionPublicId
     * @param {string} discussion.discussionTitle
     * @param {string} discussion.discussionSlug
     * @param {string} discussion.spacePublicId
     * @param {string} discussion.spaceSlug
     * @param {string} discussion.spaceName
     * @param {string} discussion.hubPublicId
     * @param {string} discussion.hubSlug
     * @param {string} discussion.hubName
     * @param {string} discussion.communityPublicId
     * @param {string} discussion.communitySlug
     * @param {string} discussion.communityName
     * @param {boolean} discussion.isDefaultCommunity
     * @param {string} discussion.lastActivityAt - ISO date string
     */
    function addToReadHistory(discussion) {
        if (!discussion || !discussion.discussionPublicId) {
            console.error('Invalid discussion data for read history');
            return;
        }

        let history = getReadHistory();

        // Remove existing entry if present (we'll add it to the top)
        history = history.filter(item => item.discussionPublicId !== discussion.discussionPublicId);

        // Create new history entry
        const entry = {
            discussionPublicId: discussion.discussionPublicId,
            discussionTitle: discussion.discussionTitle,
            discussionSlug: discussion.discussionSlug,
            spacePublicId: discussion.spacePublicId,
            spaceSlug: discussion.spaceSlug,
            spaceName: discussion.spaceName,
            hubPublicId: discussion.hubPublicId,
            hubSlug: discussion.hubSlug,
            hubName: discussion.hubName,
            communityPublicId: discussion.communityPublicId,
            communitySlug: discussion.communitySlug,
            communityName: discussion.communityName,
            isDefaultCommunity: discussion.isDefaultCommunity || false,
            lastActivityAt: discussion.lastActivityAt,
            visitedAt: new Date().toISOString()
        };

        // Add to top of history
        history.unshift(entry);

        // Limit history size
        if (history.length > MAX_HISTORY_ITEMS) {
            history = history.slice(0, MAX_HISTORY_ITEMS);
        }

        saveReadHistory(history);
    }

    /**
     * Clear all read history
     */
    function clearReadHistory() {
        try {
            localStorage.removeItem(STORAGE_KEY);
        } catch (e) {
            console.error('Failed to clear read history:', e);
        }
    }

    /**
     * Remove a specific discussion from read history
     * @param {string} discussionPublicId
     */
    function removeFromReadHistory(discussionPublicId) {
        let history = getReadHistory();
        history = history.filter(item => item.discussionPublicId !== discussionPublicId);
        saveReadHistory(history);
    }

    /**
     * Build a discussion URL from history entry
     * @param {Object} entry - History entry
     * @returns {string} Full URL path to discussion
     */
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
