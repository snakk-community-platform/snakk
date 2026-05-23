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

        const titleText = document.createElement('span');
        titleText.className = 'breadcrumb-title-text';
        titleText.textContent = titleEl.textContent?.trim() ?? '';
        titleSpan.appendChild(titleText);

        // On discussion pages, add action buttons to the breadcrumb title
        const firstPostReactions = main?.querySelector('[id^="reactions-"]') as HTMLElement | null;
        if (firstPostReactions) {
            const actionsWrapper = document.createElement('div');
            actionsWrapper.className = 'breadcrumb-actions';

            // --- Reactions (mirrored from first post) ---
            const breadcrumbReactions = document.createElement('div');
            breadcrumbReactions.className = 'breadcrumb-reactions';

            for (const attr of Array.from(firstPostReactions.attributes)) {
                if (attr.name === 'id' || attr.name === 'class') continue;
                breadcrumbReactions.setAttribute(attr.name, attr.value);
            }

            function syncReactions(): void {
                breadcrumbReactions.innerHTML = firstPostReactions!.innerHTML;
            }
            syncReactions();

            const reactObserver = new MutationObserver(syncReactions);
            reactObserver.observe(firstPostReactions, { childList: true, subtree: true, characterData: true });
            actionsWrapper.appendChild(breadcrumbReactions);

            // --- Follow button (mirrored from original) ---
            const followBtn = document.getElementById('follow-btn') as HTMLElement | null;
            if (followBtn) {
                const bcFollow = document.createElement('button');
                bcFollow.className = 'breadcrumb-action-btn';
                bcFollow.type = 'button';
                bcFollow.title = 'Follow discussion';

                for (const attr of Array.from(followBtn.attributes)) {
                    if (attr.name === 'id' || attr.name === 'class' || attr.name === 'type') continue;
                    bcFollow.setAttribute(attr.name, attr.value);
                }

                function syncFollow(): void {
                    const isFollowing = followBtn!.classList.contains('btn-primary');
                    bcFollow.innerHTML = isFollowing
                        ? '<span class="icon icon-check h-4 w-4" aria-hidden="true"></span>'
                        : '<span class="icon icon-bell h-4 w-4" aria-hidden="true"></span>';
                    bcFollow.classList.toggle('active', isFollowing);
                    bcFollow.title = isFollowing ? 'Unfollow discussion' : 'Follow discussion';
                }
                syncFollow();

                const followObserver = new MutationObserver(syncFollow);
                followObserver.observe(followBtn, { attributes: true, childList: true, subtree: true });
                actionsWrapper.appendChild(bcFollow);
            }

            // --- Share button (opens the original share dropdown) ---
            const shareBtn = document.getElementById('share-dropdown-btn') as HTMLElement | null;
            if (shareBtn) {
                const bcShare = document.createElement('button');
                bcShare.className = 'breadcrumb-action-btn';
                bcShare.type = 'button';
                bcShare.title = 'Share';
                bcShare.innerHTML = '<span class="icon icon-share h-4 w-4" aria-hidden="true"></span>';
                bcShare.addEventListener('click', () => shareBtn.focus());
                actionsWrapper.appendChild(bcShare);
            }

            titleSpan.appendChild(actionsWrapper);
        }

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
