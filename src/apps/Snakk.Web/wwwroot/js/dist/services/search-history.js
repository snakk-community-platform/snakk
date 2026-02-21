"use strict";
/**
 * Search History Manager
 * Tracks search queries, filters, and clicked results
 */
// ============================================================================
// Implementation
// ============================================================================
(function () {
    'use strict';
    const STORAGE_KEY = 'snakk_search_history';
    const MAX_HISTORY = 20;
    function getSearchHistory() {
        try {
            return JSON.parse(localStorage.getItem(STORAGE_KEY) || '[]');
        }
        catch (e) {
            console.error('Failed to load search history:', e);
            return [];
        }
    }
    function saveSearchHistory(history) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(history));
        }
        catch (e) {
            console.error('Failed to save search history:', e);
        }
    }
    function addSearchQuery(query, filters = {}) {
        if (!query || query.trim().length === 0)
            return;
        let history = getSearchHistory();
        // Remove duplicate if exists
        history = history.filter(item => item.query !== query || JSON.stringify(item.filters) !== JSON.stringify(filters));
        const entry = {
            query: query.trim(),
            filters,
            searchedAt: Date.now(),
            clickCount: 0,
            lastClickedAt: null
        };
        history.unshift(entry);
        if (history.length > MAX_HISTORY) {
            history = history.slice(0, MAX_HISTORY);
        }
        saveSearchHistory(history);
    }
    function recordResultClick(query, resultId, resultType) {
        let history = getSearchHistory();
        const entry = history.find(item => item.query === query);
        if (entry) {
            entry.clickCount = (entry.clickCount || 0) + 1;
            entry.lastClickedAt = Date.now();
            if (!entry.clickedResults) {
                entry.clickedResults = [];
            }
            const existingClick = entry.clickedResults.find(r => r.id === resultId);
            if (existingClick) {
                existingClick.count++;
                existingClick.lastClickedAt = Date.now();
            }
            else {
                entry.clickedResults.push({
                    id: resultId,
                    type: resultType,
                    count: 1,
                    lastClickedAt: Date.now()
                });
            }
            if (entry.clickedResults.length > 10) {
                entry.clickedResults.sort((a, b) => b.count - a.count);
                entry.clickedResults = entry.clickedResults.slice(0, 10);
            }
            saveSearchHistory(history);
        }
    }
    function getRecentQueries(limit = 10) {
        const history = getSearchHistory();
        return history.slice(0, limit).map(item => item.query);
    }
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
    function getSuggestions(partial, limit = 5) {
        if (!partial || partial.length < 2)
            return [];
        const history = getSearchHistory();
        const lowerPartial = partial.toLowerCase();
        return history
            .filter(item => item.query.toLowerCase().includes(lowerPartial))
            .slice(0, limit)
            .map(item => item.query);
    }
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
    function removeQuery(query) {
        let history = getSearchHistory();
        history = history.filter(item => item.query !== query);
        saveSearchHistory(history);
    }
    function clearSearchHistory() {
        localStorage.removeItem(STORAGE_KEY);
    }
    function getQueryEntry(query) {
        const history = getSearchHistory();
        return history.find(item => item.query === query) || null;
    }
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
//# sourceMappingURL=search-history.js.map