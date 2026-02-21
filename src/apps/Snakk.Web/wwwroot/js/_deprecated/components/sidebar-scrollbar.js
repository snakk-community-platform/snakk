// Sidebar Scrollbar Detection
(function() {
    'use strict';

    function checkSidebarScrollbar() {
        const sidebar = document.getElementById('sidebar');
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
