/**
 * Frontpage Discussion List - Scroll-to-Top & Counter
 * Infinite scroll is handled by HTMX (hx-trigger="revealed").
 * Discussion previews are handled by frontpage.ts via event delegation.
 */

(function(): void {
    'use strict';

    class FrontpageDiscussions {
        private scrollToTopBtn: HTMLElement | null;
        private scrollCounter: HTMLElement | null;
        private initialDiscussionCount: number;

        constructor() {
            this.scrollToTopBtn = null;
            this.scrollCounter = null;
            this.initialDiscussionCount = 0;
        }

        init(): void {
            const container = document.getElementById('discussions-container');
            if (!container) return;

            this.initialDiscussionCount = container.querySelectorAll('.topic-item-wrapper').length;
            this.initScrollToTop();
        }

        initScrollToTop(): void {
            const scrollWrapper = document.getElementById('scroll-to-top-wrapper');
            this.scrollToTopBtn = document.getElementById('scroll-to-top-btn');
            this.scrollCounter = document.getElementById('scroll-counter');

            if (!scrollWrapper || !this.scrollToTopBtn) return;

            this.updateScrollCounter();

            let scrollTimeout: number | undefined;
            window.addEventListener('scroll', () => {
                if (scrollTimeout !== undefined) {
                    window.cancelAnimationFrame(scrollTimeout);
                }
                scrollTimeout = window.requestAnimationFrame(() => {
                    this.handleScrollPosition();
                });
            }, { passive: true });

            this.scrollToTopBtn.addEventListener('click', () => {
                window.scrollTo({ top: 0, behavior: 'smooth' });
            });

            this.handleScrollPosition();
        }

        handleScrollPosition(): void {
            const scrollWrapper = document.getElementById('scroll-to-top-wrapper');
            if (!scrollWrapper) return;

            const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
            const container = document.getElementById('discussions-container');
            if (!container) return;

            const totalDiscussions = container.querySelectorAll('.topic-item-wrapper').length;
            const shouldShow = scrollTop > 800 || totalDiscussions > this.initialDiscussionCount;

            if (shouldShow) {
                scrollWrapper.classList.remove('hidden');
            } else {
                scrollWrapper.classList.add('hidden');
            }
        }

        updateScrollCounter(): void {
            if (!this.scrollCounter) return;

            const container = document.getElementById('discussions-container');
            if (!container) return;

            this.scrollCounter.textContent = container.querySelectorAll('.topic-item-wrapper').length.toString();
        }

        destroy(): void {
            this.scrollToTopBtn = null;
            this.scrollCounter = null;
        }
    }

    // Export — always create fresh instance on re-execution (SPA navigation)
    (window as any).FrontpageDiscussions = FrontpageDiscussions;
    (window as any).SnakkFrontpageDiscussions = new FrontpageDiscussions();

    // Initialize immediately if DOM is ready, otherwise wait
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            (window as any).SnakkFrontpageDiscussions.init();
        });
    } else {
        (window as any).SnakkFrontpageDiscussions.init();
    }

    // Update counter and scroll position when HTMX loads new discussion batches
    document.body.addEventListener('htmx:afterSwap', () => {
        if (document.getElementById('discussions-container')) {
            (window as any).SnakkFrontpageDiscussions.updateScrollCounter();
            (window as any).SnakkFrontpageDiscussions.handleScrollPosition();
        }
    });
})();
