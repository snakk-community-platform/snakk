// Search Page Radio Group Handler
(function() {
    'use strict';

    // Only select radios within the search filters (not navbar)
    const searchTypeRadios = document.querySelectorAll('#search-filters input[name="searchType"]');
    const dateRangeRadios = document.querySelectorAll('#search-filters input[name="dateRange"]');

    if (!searchTypeRadios.length) {
        return; // Not on search page
    }

    // Enable/disable date range based on search type
    function updateDateRangeState() {
        const selectedType = document.querySelector('#search-filters input[name="searchType"]:checked');
        const type = selectedType?.getAttribute('aria-label')?.toLowerCase();

        // Disable date range for user and space searches
        const shouldDisable = type === 'user' || type === 'space';

        dateRangeRadios.forEach((button, index) => {
            button.disabled = shouldDisable;

            if (shouldDisable) {
                // Deselect all date range buttons when disabled
                button.checked = false;
                button.parentElement.classList.add('btn-disabled', 'opacity-50', 'cursor-not-allowed');
            } else {
                button.parentElement.classList.remove('btn-disabled', 'opacity-50', 'cursor-not-allowed');
                // Auto-select first option (Today) when enabled if nothing is selected
                if (index === 0 && !document.querySelector('#search-filters input[name="dateRange"]:checked')) {
                    button.checked = true;
                }
            }
        });
    }

    // Initialize on page load
    function initialize() {
        // Get search type and date range from URL
        const urlParams = new URLSearchParams(window.location.search);
        const searchType = urlParams.get('searchType')?.toLowerCase() || 'post';
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

        // Initialize date range state
        updateDateRangeState();
    }

    // Event listeners - only update date range state when search type changes
    searchTypeRadios.forEach(radio => {
        radio.addEventListener('change', function() {
            updateDateRangeState();
        });
    });

    // Initialize on page load
    initialize();

    // Close all popups on HTMX navigation
    document.addEventListener('htmx:beforeSwap', function() {
        document.querySelectorAll('.snakk-popup').forEach(popup => {
            popup.classList.add('hidden');
        });
    });
})();
