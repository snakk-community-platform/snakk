/**
 * Breadcrumb Page Title
 * Shows the page title in the breadcrumb bar when scrolling past it
 */

// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    let scrollHandler: (() => void) | null = null;

    function initBreadcrumbTitle(): void {
        // Clean up previous scroll handler
        if (scrollHandler) {
            window.removeEventListener('scroll', scrollHandler);
            scrollHandler = null;
        }

        const bar = document.querySelector('.breadcrumb-bar') as HTMLElement | null;
        if (!bar) return;

        // Remove stale title element from previous page
        const existing = bar.querySelector('.breadcrumb-page-title');
        if (existing) {
            bar.classList.remove('show-title');
            existing.remove();
        }

        // Find the page title — .page-header h1 first, then any h1 in main-content
        const main = document.getElementById('main-content');
        const titleEl = main?.querySelector('.page-header h1') as HTMLElement | null
            || main?.querySelector('h1') as HTMLElement | null;
        if (!titleEl) return;

        // Don't show if title is inside the breadcrumb bar itself
        if (bar.contains(titleEl)) return;

        // Create the title element (inner span needed for CSS grid 0fr/1fr transition)
        const titleLine = document.createElement('div');
        titleLine.className = 'breadcrumb-page-title';
        const titleSpan = document.createElement('span');
        titleSpan.textContent = titleEl.textContent?.trim() ?? '';
        titleLine.appendChild(titleSpan);
        bar.appendChild(titleLine);

        const sidebarInner = document.getElementById('sidebar-inner');

        let ticking = false;
        function checkTitle(): void {
            const barRect = bar!.getBoundingClientRect();
            const titleRect = titleEl!.getBoundingClientRect();
            const titleMidpoint = titleRect.top + titleRect.height / 2;

            if (titleMidpoint <= barRect.bottom) {
                bar!.classList.add('show-title');
                sidebarInner?.classList.add('breadcrumb-expanded');
            } else {
                bar!.classList.remove('show-title');
                sidebarInner?.classList.remove('breadcrumb-expanded');
            }
            ticking = false;
        }

        scrollHandler = function(): void {
            if (!ticking) {
                ticking = true;
                requestAnimationFrame(checkTitle);
            }
        };

        window.addEventListener('scroll', scrollHandler, { passive: true });
        checkTitle();
    }

    // Run on load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initBreadcrumbTitle);
    } else {
        initBreadcrumbTitle();
    }

    // Re-run after HTMX navigation
    document.addEventListener('htmx:afterSwap', function(evt: Event): void {
        const detail = (evt as CustomEvent).detail;
        if (detail?.target?.id === 'main-content') {
            initBreadcrumbTitle();
        }
    });
})();
