/**
 * Search History Manager
 * Tracks search queries, filters, and clicked results
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface SearchFilters {
    [key: string]: string | number | boolean;
}

interface ClickedResult {
    id: string;
    type: string;
    count: number;
    lastClickedAt: number;
}

interface SearchHistoryEntry {
    query: string;
    filters: SearchFilters;
    searchedAt: number;
    clickCount: number;
    lastClickedAt: number | null;
    clickedResults?: ClickedResult[];
}

interface PopularQuery {
    query: string;
    clickCount: number;
    lastClickedAt: number | null;
}

interface SnakkSearchHistoryAPI {
    addSearchQuery(query: string, filters?: SearchFilters): void;
    recordResultClick(query: string, resultId: string, resultType: string): void;
    getRecentQueries(limit?: number): string[];
    getPopularQueries(limit?: number): PopularQuery[];
    getSuggestions(partial: string, limit?: number): string[];
    getCommonFilters(): Record<string, number>;
    removeQuery(query: string): void;
    clearSearchHistory(): void;
    getQueryEntry(query: string): SearchHistoryEntry | null;
    pruneOldSearches(): void;
}


// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    const STORAGE_KEY = 'snakk_search_history';
    const MAX_HISTORY = 20;

    function getSearchHistory(): SearchHistoryEntry[] {
        try {
            return JSON.parse(localStorage.getItem(STORAGE_KEY) || '[]') as SearchHistoryEntry[];
        } catch (e) {
            console.error('Failed to load search history:', e);
            return [];
        }
    }

    function saveSearchHistory(history: SearchHistoryEntry[]): void {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(history));
        } catch (e) {
            console.error('Failed to save search history:', e);
        }
    }

    function addSearchQuery(query: string, filters: SearchFilters = {}): void {
        if (!query || query.trim().length === 0) return;

        let history = getSearchHistory();

        // Remove duplicate if exists
        history = history.filter(item =>
            item.query !== query || JSON.stringify(item.filters) !== JSON.stringify(filters)
        );

        const entry: SearchHistoryEntry = {
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

    function recordResultClick(query: string, resultId: string, resultType: string): void {
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
            } else {
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

    function getRecentQueries(limit: number = 10): string[] {
        const history = getSearchHistory();
        return history.slice(0, limit).map(item => item.query);
    }

    function getPopularQueries(limit: number = 10): PopularQuery[] {
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

    function getSuggestions(partial: string, limit: number = 5): string[] {
        if (!partial || partial.length < 2) return [];

        const history = getSearchHistory();
        const lowerPartial = partial.toLowerCase();

        return history
            .filter(item => item.query.toLowerCase().includes(lowerPartial))
            .slice(0, limit)
            .map(item => item.query);
    }

    function getCommonFilters(): Record<string, number> {
        const history = getSearchHistory();
        const filterCounts: Record<string, number> = {};

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

    function removeQuery(query: string): void {
        let history = getSearchHistory();
        history = history.filter(item => item.query !== query);
        saveSearchHistory(history);
    }

    function clearSearchHistory(): void {
        localStorage.removeItem(STORAGE_KEY);
    }

    function getQueryEntry(query: string): SearchHistoryEntry | null {
        const history = getSearchHistory();
        return history.find(item => item.query === query) || null;
    }

    function pruneOldSearches(): void {
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
    (window as any).SnakkSearchHistory = {
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
