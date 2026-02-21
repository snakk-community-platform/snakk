/**
 * Search History Manager
 * Tracks search queries, filters, and clicked results
 */
(function() {
    'use strict';

    const STORAGE_KEY = 'snakk_search_history';
    const MAX_HISTORY = 20;

    /**
     * Get all search history
     * @returns {Array}
     */
    function getSearchHistory() {
        try {
            return JSON.parse(localStorage.getItem(STORAGE_KEY) || '[]');
        } catch (e) {
            console.error('Failed to load search history:', e);
            return [];
        }
    }

    /**
     * Save search history
     * @param {Array} history
     */
    function saveSearchHistory(history) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(history));
        } catch (e) {
            console.error('Failed to save search history:', e);
        }
    }

    /**
     * Add a search query to history
     * @param {string} query
     * @param {Object} filters - Optional filters used (e.g., { type: 'discussion', spaceId: 'abc' })
     */
    function addSearchQuery(query, filters = {}) {
        if (!query || query.trim().length === 0) return;

        let history = getSearchHistory();

        // Remove duplicate if exists (move to top)
        history = history.filter(item =>
            item.query !== query || JSON.stringify(item.filters) !== JSON.stringify(filters)
        );

        // Add to top
        const entry = {
            query: query.trim(),
            filters,
            searchedAt: Date.now(),
            clickCount: 0,
            lastClickedAt: null
        };

        history.unshift(entry);

        // Limit to MAX_HISTORY items
        if (history.length > MAX_HISTORY) {
            history = history.slice(0, MAX_HISTORY);
        }

        saveSearchHistory(history);
    }

    /**
     * Record a click on a search result
     * @param {string} query
     * @param {string} resultId - ID of the clicked result (discussionId, postId, userId)
     * @param {string} resultType - Type of result ('discussion', 'post', 'user')
     */
    function recordResultClick(query, resultId, resultType) {
        let history = getSearchHistory();

        // Find the matching query
        const entry = history.find(item => item.query === query);
        if (entry) {
            entry.clickCount = (entry.clickCount || 0) + 1;
            entry.lastClickedAt = Date.now();

            // Track clicked results for ranking
            if (!entry.clickedResults) {
                entry.clickedResults = [];
            }

            // Add or update clicked result
            const existingClick = entry.clickedResults.find(r => r.id === resultId);
            if (existingClick) {
                existingClick.count++;
                existingClick.lastClickedAt = Date.now();
            } else {
                entry.clickedResults.push({
                    id: resultId,
                    type: resultType,
                    count: 1,
                    lastClickedAt: Date.now()
                });
            }

            // Limit clicked results to 10 per query
            if (entry.clickedResults.length > 10) {
                entry.clickedResults.sort((a, b) => b.count - a.count);
                entry.clickedResults = entry.clickedResults.slice(0, 10);
            }

            saveSearchHistory(history);
        }
    }

    /**
     * Get recent search queries (no duplicates)
     * @param {number} limit
     * @returns {Array<string>}
     */
    function getRecentQueries(limit = 10) {
        const history = getSearchHistory();
        return history.slice(0, limit).map(item => item.query);
    }

    /**
     * Get popular search queries (sorted by click count)
     * @param {number} limit
     * @returns {Array}
     */
    function getPopularQueries(limit = 10) {
        const history = getSearchHistory();
        return history
            .filter(item => item.clickCount > 0)
            .sort((a, b) => b.clickCount - a.clickCount)
            .slice(0, limit)
            .map(item => ({
                query: item.query,
                clickCount: item.clickCount,
                lastClickedAt: item.lastClickedAt
            }));
    }

    /**
     * Get suggested queries based on partial input
     * @param {string} partial
     * @param {number} limit
     * @returns {Array<string>}
     */
    function getSuggestions(partial, limit = 5) {
        if (!partial || partial.length < 2) return [];

        const history = getSearchHistory();
        const lowerPartial = partial.toLowerCase();

        return history
            .filter(item => item.query.toLowerCase().includes(lowerPartial))
            .slice(0, limit)
            .map(item => item.query);
    }

    /**
     * Get commonly used filters
     * @returns {Object} Map of filter key to count
     */
    function getCommonFilters() {
        const history = getSearchHistory();
        const filterCounts = {};

        history.forEach(item => {
            if (item.filters) {
                Object.entries(item.filters).forEach(([key, value]) => {
                    const filterKey = `${key}:${value}`;
                    filterCounts[filterKey] = (filterCounts[filterKey] || 0) + 1;
                });
            }
        });

        return filterCounts;
    }

    /**
     * Remove a specific query from history
     * @param {string} query
     */
    function removeQuery(query) {
        let history = getSearchHistory();
        history = history.filter(item => item.query !== query);
        saveSearchHistory(history);
    }

    /**
     * Clear all search history
     */
    function clearSearchHistory() {
        localStorage.removeItem(STORAGE_KEY);
    }

    /**
     * Get full entry for a query (with metadata)
     * @param {string} query
     * @returns {Object|null}
     */
    function getQueryEntry(query) {
        const history = getSearchHistory();
        return history.find(item => item.query === query) || null;
    }

    /**
     * Prune old searches (older than 90 days)
     */
    function pruneOldSearches() {
        let history = getSearchHistory();
        const maxAge = 90 * 24 * 60 * 60 * 1000;
        const now = Date.now();

        history = history.filter(item => {
            const age = now - item.searchedAt;
            return age <= maxAge;
        });

        saveSearchHistory(history);
    }

    // Prune on load
    pruneOldSearches();

    // Export API
    window.SnakkSearchHistory = {
        addSearchQuery,
        recordResultClick,
        getRecentQueries,
        getPopularQueries,
        getSuggestions,
        getCommonFilters,
        removeQuery,
        clearSearchHistory,
        getQueryEntry,
        pruneOldSearches
    };
})();
