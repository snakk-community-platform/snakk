/**
 * Frontpage (Index) JavaScript
 * Manages sticky sidebar and discussion previews
 */

// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    /**
     * Sticky Sidebar Feature (desktop only)
     */
    function initStickySidebar(): void {
        // Only run on desktop (lg breakpoint)
        if (window.innerWidth < 1024) return;

        const sidebar = document.getElementById('sidebar');
        const nav = document.querySelector<HTMLElement>('nav');

        if (!sidebar || !nav) return;

        let sidebarOriginalTop: number | null = null;
        let navHeight = 0;
        let isSticky = false;

        function updateMeasurements(): void {
            if (!sidebar || !nav) return;
            navHeight = nav.offsetHeight;
            const sidebarRect = sidebar.getBoundingClientRect();
            const scrollTop = window.pageYOffset || document.documentElement.scrollTop;

            if (sidebarOriginalTop === null) {
                sidebarOriginalTop = sidebarRect.top + scrollTop;
            }

            // Set max-height to viewport minus nav height
            sidebar.style.maxHeight = `calc(100vh - ${navHeight}px)`;
        }

        function handleScroll(): void {
            if (!sidebar || sidebarOriginalTop === null) return;
            const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
            const triggerPoint = sidebarOriginalTop - navHeight;

            if (scrollTop >= triggerPoint && !isSticky) {
                // Make sticky
                sidebar.classList.add('sidebar-sticky');
                sidebar.style.top = `calc(${navHeight}px + 1rem)`;
                isSticky = true;
            } else if (scrollTop < triggerPoint && isSticky) {
                // Remove sticky
                sidebar.classList.remove('sidebar-sticky');
                sidebar.style.top = '';
                isSticky = false;
            }
        }

        // Initialize
        updateMeasurements();
        handleScroll();

        // Listen to scroll events (throttled)
        let scrollTimeout: number;
        window.addEventListener('scroll', function() {
            if (scrollTimeout) {
                window.cancelAnimationFrame(scrollTimeout);
            }
            scrollTimeout = window.requestAnimationFrame(function() {
                handleScroll();
            });
        }, { passive: true });

        // Update measurements on resize
        let resizeTimeout: ReturnType<typeof setTimeout>;
        window.addEventListener('resize', function() {
            clearTimeout(resizeTimeout);
            resizeTimeout = setTimeout(function() {
                if (!sidebar) return;
                if (window.innerWidth >= 1024) {
                    sidebarOriginalTop = null; // Reset to recalculate
                    updateMeasurements();
                    handleScroll();
                } else {
                    // Remove sticky on mobile
                    sidebar.classList.remove('sidebar-sticky');
                    sidebar.style.top = '';
                    sidebar.style.maxHeight = '';
                    isSticky = false;
                }
            }, 100);
        });
    }

    /**
     * Discussion Preview Feature
     * Uses event delegation on #discussions-container so HTMX-loaded items work automatically.
     */
    function initDiscussionPreviews(): void {
        const container = document.getElementById('discussions-container');
        if (!container) return;

        const previewCache = new Map<string, string>();

        function truncateText(text: string, maxLength: number): string {
            if (text.length <= maxLength) return text;

            let truncated = text.substring(0, maxLength);
            const lastSpace = truncated.lastIndexOf(' ');

            if (lastSpace > 0) {
                truncated = truncated.substring(0, lastSpace);
            }

            return truncated + '...';
        }

        async function fetchPreview(discussionId: string): Promise<string | null> {
            if (previewCache.has(discussionId)) {
                return previewCache.get(discussionId) || null;
            }

            try {
                const response = await fetch(`/bff/discussions/${discussionId}/preview`);
                if (!response.ok) {
                    throw new Error('Failed to fetch preview');
                }

                const data = await response.json() as { content: string };
                previewCache.set(discussionId, data.content);
                return data.content;
            } catch (error) {
                console.error('Error fetching preview:', error);
                return null;
            }
        }

        function togglePreview(button: HTMLElement, previewDiv: HTMLElement, discussionId: string): void {
            const isCurrentlyVisible = !previewDiv.classList.contains('hidden');

            if (isCurrentlyVisible) {
                previewDiv.classList.add('hidden');
                button.classList.remove('active');
            } else {
                const previewContent = previewDiv.querySelector<HTMLElement>('.preview-content');
                if (!previewContent) return;

                if (previewContent.textContent) {
                    previewDiv.classList.remove('hidden');
                    button.classList.add('active');
                } else {
                    previewContent.innerHTML = '<span class="loading loading-spinner loading-sm"></span>';
                    previewDiv.classList.remove('hidden');
                    button.classList.add('active');

                    fetchPreview(discussionId).then(content => {
                        if (content) {
                            previewContent.textContent = truncateText(content, 480);
                        } else {
                            previewContent.textContent = 'Failed to load preview';
                        }
                    });
                }
            }
        }

        // Event delegation: catches clicks on both initial and HTMX-loaded preview buttons
        container.addEventListener('click', (e: MouseEvent) => {
            const target = e.target as HTMLElement;
            const button = target.closest('.preview-btn') as HTMLElement | null;
            if (!button || !button.dataset.discussionId) return;

            e.preventDefault();
            const discussionId = button.dataset.discussionId;
            const wrapper = button.closest('.topic-item-wrapper');
            const previewDiv = wrapper?.nextElementSibling as HTMLElement | null;

            if (previewDiv && previewDiv.classList.contains('discussion-preview')) {
                togglePreview(button, previewDiv, discussionId);
            }
        });
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            initStickySidebar();
            initDiscussionPreviews();
        });
    } else {
        initStickySidebar();
        initDiscussionPreviews();
    }
})();
