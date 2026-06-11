/**
 * Search Page Radio Group Handler
 * Manages search type and date range filter states
 */

// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    // Only select radios within the search filters (not navbar)
    const searchTypeRadios = document.querySelectorAll<HTMLInputElement>('#search-filters input[name="searchType"]');
    const dateRangeRadios = document.querySelectorAll<HTMLInputElement>('#search-filters input[name="dateRange"]');

    if (!searchTypeRadios.length) {
        return; // Not on search page
    }

    /**
     * Initialize on page load
     */
    function initialize(): void {
        // Get search type and date range from URL
        const urlParams = new URLSearchParams(window.location.search);
        const searchType = urlParams.get('searchType')?.toLowerCase() || 'discussion';
        const dateRange = urlParams.get('dateRange')?.toLowerCase();

        // Set search type radio button
        searchTypeRadios.forEach(radio => {
            const value = radio.value?.toLowerCase();
            if (value === searchType) {
                radio.checked = true;
            }
        });

        // Set date range radio button
        if (dateRange) {
            dateRangeRadios.forEach(radio => {
                const value = radio.value?.toLowerCase();
                if (value === dateRange) {
                    radio.checked = true;
                }
            });
        }

    }

    // Initialize on page load
    initialize();

    // Close all popups on HTMX navigation
    document.addEventListener('htmx:beforeSwap', function() {
        document.querySelectorAll<HTMLElement>('.snakk-popup').forEach(popup => {
            popup.classList.add('hidden');
        });
    });
})();
