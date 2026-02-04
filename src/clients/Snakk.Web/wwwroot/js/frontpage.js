// Frontpage (Index) JavaScript

(function() {
    'use strict';

    // Sticky Sidebar Feature (desktop only)
    function initStickySidebar() {
        // Only run on desktop (lg breakpoint)
        if (window.innerWidth < 1024) return;

        const sidebar = document.getElementById('sidebar');
        const nav = document.querySelector('nav');

        if (!sidebar || !nav) return;

        let sidebarOriginalTop = null;
        let navHeight = 0;
        let isSticky = false;

        function updateMeasurements() {
            navHeight = nav.offsetHeight;
            const sidebarRect = sidebar.getBoundingClientRect();
            const scrollTop = window.pageYOffset || document.documentElement.scrollTop;

            if (sidebarOriginalTop === null) {
                sidebarOriginalTop = sidebarRect.top + scrollTop;
            }

            // Set max-height to viewport minus nav height
            sidebar.style.maxHeight = `calc(100vh - ${navHeight}px)`;
        }

        function handleScroll() {
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
        let scrollTimeout;
        window.addEventListener('scroll', function() {
            if (scrollTimeout) {
                window.cancelAnimationFrame(scrollTimeout);
            }
            scrollTimeout = window.requestAnimationFrame(function() {
                handleScroll();
            });
        }, { passive: true });

        // Update measurements on resize
        let resizeTimeout;
        window.addEventListener('resize', function() {
            clearTimeout(resizeTimeout);
            resizeTimeout = setTimeout(function() {
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

    // Discussion Preview Feature
    function initDiscussionPreviews() {
        const previewCache = new Map();
        const apiBaseUrl = window.SnakkConfig?.apiBaseUrl || '';

        function truncateText(text, maxLength) {
            if (text.length <= maxLength) return text;

            // Find the last space before maxLength
            let truncated = text.substring(0, maxLength);
            const lastSpace = truncated.lastIndexOf(' ');

            if (lastSpace > 0) {
                truncated = truncated.substring(0, lastSpace);
            }

            return truncated + '...';
        }

        async function fetchPreview(discussionId) {
            if (previewCache.has(discussionId)) {
                return previewCache.get(discussionId);
            }

            try {
                const response = await fetch(`${apiBaseUrl}/discussions/${discussionId}/preview`);
                if (!response.ok) {
                    throw new Error('Failed to fetch preview');
                }

                const data = await response.json();
                previewCache.set(discussionId, data.content);
                return data.content;
            } catch (error) {
                console.error('Error fetching preview:', error);
                return null;
            }
        }

        function togglePreview(button, previewDiv, discussionId) {
            const isCurrentlyVisible = !previewDiv.classList.contains('hidden');

            if (isCurrentlyVisible) {
                // Hide preview
                previewDiv.classList.add('hidden');
                button.classList.remove('active');
            } else {
                // Show preview
                const previewContent = previewDiv.querySelector('.preview-content');

                if (previewContent.textContent) {
                    // Already loaded, just show
                    previewDiv.classList.remove('hidden');
                    button.classList.add('active');
                } else {
                    // Load and show
                    previewContent.innerHTML = '<span class="loading loading-spinner loading-sm"></span>';
                    previewDiv.classList.remove('hidden');
                    button.classList.add('active');

                    fetchPreview(discussionId).then(content => {
                        if (content) {
                            const truncated = truncateText(content, 480);
                            previewContent.textContent = truncated;
                        } else {
                            previewContent.textContent = 'Failed to load preview';
                        }
                    });
                }
            }
        }

        // Attach click handlers to all preview buttons
        document.querySelectorAll('.preview-btn').forEach(button => {
            button.addEventListener('click', function(e) {
                e.preventDefault();
                e.stopPropagation(); // Prevent bubbling to event delegation handler
                const discussionId = this.dataset.discussionId;
                const wrapper = this.closest('.topic-item-wrapper');
                const previewDiv = wrapper.nextElementSibling;

                if (previewDiv && previewDiv.classList.contains('discussion-preview')) {
                    togglePreview(this, previewDiv, discussionId);
                }
            });
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
