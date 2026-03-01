"use strict";
/**
 * Breadcrumb Page Title
 * Shows the page title in the breadcrumb bar when scrolling past it
 */
// ============================================================================
// Implementation
// ============================================================================
(function () {
    'use strict';
    let scrollHandler = null;
    function initBreadcrumbTitle() {
        // Clean up previous scroll handler
        if (scrollHandler) {
            window.removeEventListener('scroll', scrollHandler);
            scrollHandler = null;
        }
        const bar = document.querySelector('.breadcrumb-bar');
        if (!bar)
            return;
        // Remove stale title element from previous page
        const existing = bar.querySelector('.breadcrumb-page-title');
        if (existing) {
            bar.classList.remove('show-title');
            existing.remove();
        }
        // Find the page title — .page-header h1 first, then any h1 in main-content
        const main = document.getElementById('main-content');
        const titleEl = main?.querySelector('.page-header h1')
            || main?.querySelector('h1');
        if (!titleEl)
            return;
        // Don't show if title is inside the breadcrumb bar itself
        if (bar.contains(titleEl))
            return;
        // Create the title element
        const titleLine = document.createElement('div');
        titleLine.className = 'breadcrumb-page-title';
        titleLine.textContent = titleEl.textContent?.trim() ?? '';
        bar.appendChild(titleLine);
        scrollHandler = function () {
            const barRect = bar.getBoundingClientRect();
            const titleRect = titleEl.getBoundingClientRect();
            const titleMidpoint = titleRect.top + titleRect.height / 2;
            if (titleMidpoint <= barRect.bottom) {
                bar.classList.add('show-title');
            }
            else {
                bar.classList.remove('show-title');
            }
        };
        window.addEventListener('scroll', scrollHandler, { passive: true });
        scrollHandler();
    }
    // Run on load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initBreadcrumbTitle);
    }
    else {
        initBreadcrumbTitle();
    }
    // Re-run after HTMX navigation
    document.addEventListener('htmx:afterSwap', function (evt) {
        const detail = evt.detail;
        if (detail?.target?.id === 'main-content') {
            initBreadcrumbTitle();
        }
    });
})();
//# sourceMappingURL=breadcrumb-title.js.map