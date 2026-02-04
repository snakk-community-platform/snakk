// Search Focus Pane
(function() {
    'use strict';

    const searchInput = document.getElementById('search-input');
    const searchPane = document.getElementById('search-focus-pane');
    const searchWrapper = document.getElementById('search-wrapper');

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
    const searchTypeText = {
        'post': 'post bodies',
        'discussion': 'discussion titles',
        'space': 'space names',
        'user': 'user names'
    };

    // Update placeholder based on selected search type and date range
    function updatePlaceholder() {
        const selectedType = document.querySelector('input[name="search-type"]:checked');

        if (selectedType && searchInput.matches(':focus')) {
            const type = selectedType.getAttribute('aria-label')?.toLowerCase();
            const typeText = searchTypeText[type];

            if (!typeText) {
                searchInput.placeholder = 'Search...';
                return;
            }

            // For user and space searches (no date range)
            if (type === 'user' || type === 'space') {
                searchInput.placeholder = `Search ${typeText}...`;
                return;
            }

            // For post and discussion searches (with date range)
            const selectedDateRange = document.querySelector('input[name="date-range"]:checked');
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

    // Enable/disable date range based on search type
    function updateDateRangeState() {
        const selectedType = document.querySelector('input[name="search-type"]:checked');
        const type = selectedType?.getAttribute('aria-label')?.toLowerCase();
        const dateRangeButtons = document.querySelectorAll('input[name="date-range"]');

        // Disable date range for user and space searches
        const shouldDisable = type === 'user' || type === 'space';

        dateRangeButtons.forEach((button, index) => {
            button.disabled = shouldDisable;

            if (shouldDisable) {
                // Deselect all date range buttons when disabled
                button.checked = false;
                button.parentElement.classList.add('btn-disabled', 'opacity-50', 'cursor-not-allowed');
            } else {
                button.parentElement.classList.remove('btn-disabled', 'opacity-50', 'cursor-not-allowed');
                // Auto-select first option (Today) when enabled
                if (index === 0) {
                    button.checked = true;
                }
            }
        });
    }

    // Show pane and update placeholder when input is focused
    searchInput.addEventListener('focus', function() {
        searchPane.classList.remove('hidden');
        updatePlaceholder();
        updateDateRangeState();
    });

    // Reset placeholder when input loses focus
    searchInput.addEventListener('blur', function() {
        searchInput.placeholder = 'Search...';
    });

    // Hide pane when clicking outside
    document.addEventListener('click', function(e) {
        // If click is outside the wrapper, hide the pane
        if (!searchWrapper.contains(e.target)) {
            searchPane.classList.add('hidden');
        }
    });

    // Update placeholder and date range state when search type or date range changes
    searchPane.addEventListener('change', function(e) {
        if (e.target.name === 'search-type') {
            updatePlaceholder();
            updateDateRangeState();
        } else if (e.target.name === 'date-range') {
            updatePlaceholder();
        }
    });

    // Prevent form submission when pressing Enter on radio buttons
    searchPane.addEventListener('keydown', function(e) {
        if (e.key === 'Enter' && e.target.type === 'radio') {
            e.preventDefault();
        }
    });

    // Keep search input focused when clicking radio buttons
    searchPane.addEventListener('mousedown', function(e) {
        // Prevent radio buttons from stealing focus
        if (e.target.type === 'radio') {
            e.preventDefault();
            // Keep the search input focused
            searchInput.focus();
        }
    });

    // Handle form submission to include search type and date range
    const searchForm = document.getElementById('search-form');
    if (searchForm) {
        searchForm.addEventListener('submit', function(e) {
            const selectedType = document.querySelector('input[name="search-type"]:checked');
            const selectedDateRange = document.querySelector('input[name="date-range"]:checked');
            const query = searchInput.value.trim();

            // Only customize URL if we have a query
            if (query) {
                e.preventDefault();

                const searchType = selectedType?.getAttribute('aria-label')?.toLowerCase() || 'post';
                const dateRange = selectedDateRange?.getAttribute('aria-label')?.toLowerCase();

                // Build URL
                let url = `/search?searchType=${searchType}&q=${encodeURIComponent(query)}`;
                if (dateRange && searchType !== 'user' && searchType !== 'space') {
                    url += `&dateRange=${encodeURIComponent(dateRange)}`;
                }

                // Navigate to search page
                window.location.href = url;
            }
        });
    }
})();
