/**
 * Sidebar Scrollbar Detection
 * Adds 'has-scrollbar' class to sidebar when it has a scrollbar
 */

// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    function checkSidebarScrollbar(): void {
        const sidebar = document.getElementById('sticky-sidebar');
        if (!sidebar) return;

        // Check if sidebar has a scrollbar (scrollHeight > clientHeight)
        if (sidebar.scrollHeight > sidebar.clientHeight) {
            sidebar.classList.add('has-scrollbar');
        } else {
            sidebar.classList.remove('has-scrollbar');
        }
    }

    // Check on load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', checkSidebarScrollbar);
    } else {
        checkSidebarScrollbar();
    }

    // Check on window resize
    window.addEventListener('resize', checkSidebarScrollbar);

    // Check when content changes (for HTMX updates)
    document.addEventListener('htmx:afterSwap', checkSidebarScrollbar);
})();
