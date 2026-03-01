"use strict";
/**
 * Sidebar Scrollbar Detection
 * Adds 'has-scrollbar' class to sidebar when it has a scrollbar
 */
// ============================================================================
// Implementation
// ============================================================================
(function () {
    'use strict';
    function checkSidebarScrollbar() {
        const sidebar = document.getElementById('sticky-sidebar');
        if (!sidebar)
            return;
        // Check if sidebar has a scrollbar (scrollHeight > clientHeight)
        if (sidebar.scrollHeight > sidebar.clientHeight) {
            sidebar.classList.add('has-scrollbar');
        }
        else {
            sidebar.classList.remove('has-scrollbar');
        }
    }
    // Check on load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', checkSidebarScrollbar);
    }
    else {
        checkSidebarScrollbar();
    }
    // Check on window resize
    window.addEventListener('resize', checkSidebarScrollbar);
    // Check when content changes (for HTMX updates)
    document.addEventListener('htmx:afterSwap', checkSidebarScrollbar);
})();
//# sourceMappingURL=sidebar-scrollbar.js.map