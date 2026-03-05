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
        private scrollWrapper: HTMLElement | null;
        private container: HTMLElement | null;
        private initialDiscussionCount: number;
        private cachedDiscussionCount: number;

        constructor() {
            this.scrollToTopBtn = null;
            this.scrollCounter = null;
            this.scrollWrapper = null;
            this.container = null;
            this.initialDiscussionCount = 0;
            this.cachedDiscussionCount = 0;
        }

        init(): void {
            this.container = document.getElementById('discussions-container');
            if (!this.container) return;

            this.initialDiscussionCount = this.container.querySelectorAll('.topic-item-wrapper').length;
            this.cachedDiscussionCount = this.initialDiscussionCount;
            this.initScrollToTop();
        }

        initScrollToTop(): void {
            this.scrollWrapper = document.getElementById('scroll-to-top-wrapper');
            this.scrollToTopBtn = document.getElementById('scroll-to-top-btn');
            this.scrollCounter = document.getElementById('scroll-counter');

            if (!this.scrollWrapper || !this.scrollToTopBtn) return;

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
            if (!this.scrollWrapper) return;

            const shouldShow = window.scrollY > 800 || this.cachedDiscussionCount > this.initialDiscussionCount;

            if (shouldShow) {
                this.scrollWrapper.classList.remove('hidden');
            } else {
                this.scrollWrapper.classList.add('hidden');
            }
        }

        updateScrollCounter(): void {
            if (!this.scrollCounter || !this.container) return;

            this.cachedDiscussionCount = this.container.querySelectorAll('.topic-item-wrapper').length;
            this.scrollCounter.textContent = this.cachedDiscussionCount.toString();
        }

        destroy(): void {
            this.scrollToTopBtn = null;
            this.scrollCounter = null;
            this.scrollWrapper = null;
            this.container = null;
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
