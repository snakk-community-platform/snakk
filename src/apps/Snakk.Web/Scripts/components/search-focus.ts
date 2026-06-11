/**
 * Search Focus Pane
 * Handles search input focus behavior with type and date range filters
 */

// ============================================================================
// Type Definitions
// ============================================================================

type SearchType = 'post' | 'discussion';

interface SearchTypeTextMap {
    post: string;
    discussion: string;
}

// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    const searchInput = document.getElementById('search-input') as HTMLInputElement | null;
    const searchPane = document.getElementById('search-focus-pane') as HTMLElement | null;
    const searchWrapper = document.getElementById('search-wrapper') as HTMLElement | null;

    if (!searchInput || !searchPane) {
        return;
    }

    // Populate search input from URL query parameter (if present)
    const urlParams = new URLSearchParams(window.location.search);
    const queryParam = urlParams.get('q');
    if (queryParam && searchInput) {
        searchInput.value = queryParam;
    }

    // Disable popup on search page (user will use page radio groups instead)
    const isSearchPage = window.location.pathname === '/search' || window.location.pathname.startsWith('/Search');
    if (isSearchPage) {
        return; // Exit early, don't attach any event listeners
    }

    // Placeholder text mapping (base text for each type)
    const searchTypeText: SearchTypeTextMap = {
        post: 'post bodies',
        discussion: 'discussion titles'
    };

    /**
     * Update placeholder based on selected search type and date range
     */
    function updatePlaceholder(): void {
        if (!searchInput) return;

        const selectedType = document.querySelector('input[name="search-type"]:checked') as HTMLInputElement | null;

        if (selectedType && searchInput.matches(':focus')) {
            const type = selectedType.getAttribute('aria-label')?.toLowerCase() as SearchType | undefined;
            const typeText = type ? searchTypeText[type] : undefined;

            if (!typeText) {
                searchInput.placeholder = 'Search...';
                return;
            }

            // With date range
            const selectedDateRange = document.querySelector('input[name="date-range"]:checked') as HTMLInputElement | null;
            if (selectedDateRange) {
                const dateRange = selectedDateRange.getAttribute('aria-label')?.toLowerCase();

                if (dateRange === 'all time') {
                    searchInput.placeholder = `Search all ${typeText}...`;
                } else {
                    searchInput.placeholder = `Search ${typeText} from ${dateRange}...`;
                }
            } else {
                // Fallback if no date range selected
                searchInput.placeholder = `Search ${typeText}...`;
            }
        } else {
            searchInput.placeholder = 'Search...';
        }
    }

    // Show pane and update placeholder when input is focused
    searchInput.addEventListener('focus', function() {
        if (!searchPane) return;
        searchPane.classList.remove('hidden');
        updatePlaceholder();
    });

    // Reset placeholder when input loses focus
    searchInput.addEventListener('blur', function() {
        if (!searchInput) return;
        searchInput.placeholder = 'Search...';
    });

    // Hide pane when clicking outside
    document.addEventListener('click', function(e: MouseEvent) {
        if (!searchWrapper || !searchPane) return;
        // If click is outside the wrapper, hide the pane
        if (!searchWrapper.contains(e.target as Node)) {
            searchPane.classList.add('hidden');
        }
    });

    // Update placeholder and date range state when search type or date range changes
    searchPane.addEventListener('change', function(e: Event) {
        const target = e.target as HTMLInputElement;
        if (target.name === 'search-type') {
            updatePlaceholder();
        } else if (target.name === 'date-range') {
            updatePlaceholder();
        }
    });

    // Prevent form submission when pressing Enter on radio buttons
    searchPane.addEventListener('keydown', function(e: KeyboardEvent) {
        const target = e.target as HTMLInputElement;
        if (e.key === 'Enter' && target.type === 'radio') {
            e.preventDefault();
        }
    });

    // Keep search input focused when clicking radio buttons
    searchPane.addEventListener('mousedown', function(e: MouseEvent) {
        const target = e.target as HTMLInputElement;
        // Prevent radio buttons from stealing focus
        if (target.type === 'radio') {
            e.preventDefault();
            // Keep the search input focused
            if (searchInput) {
                searchInput.focus();
            }
        }
    });

    // Handle form submission to include search type and date range
    const searchForm = document.getElementById('search-form') as HTMLFormElement | null;
    if (searchForm) {
        searchForm.addEventListener('submit', function(e: SubmitEvent) {
            if (!searchInput) return;

            const selectedType = document.querySelector('input[name="search-type"]:checked') as HTMLInputElement | null;
            const selectedDateRange = document.querySelector('input[name="date-range"]:checked') as HTMLInputElement | null;
            const query = searchInput.value.trim();

            // Only customize URL if we have a query
            if (query) {
                e.preventDefault();

                const searchType = selectedType?.getAttribute('aria-label')?.toLowerCase() || 'post';
                const dateRange = selectedDateRange?.getAttribute('aria-label')?.toLowerCase();

                // Build URL
                let url = `/search?searchType=${searchType}&q=${encodeURIComponent(query)}`;
                if (dateRange) {
                    url += `&dateRange=${encodeURIComponent(dateRange)}`;
                }

                // Navigate to search page
                window.location.href = url;
            }
        });
    }
})();
