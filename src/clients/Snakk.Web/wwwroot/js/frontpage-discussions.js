// Frontpage Discussion List (endless scroll)

(function() {
    'use strict';

    let nextCursor = '';
    let hasMore = false;
    let homeScrollObserver = null;
    let loadMoreRequest = null;
    let previewHandlersAttached = false;
    const previewCache = new Map();
    let scrollToTopBtn = null;
    let scrollCounter = null;
    let initialDiscussionCount = 0;

    /**
     * Initialize the frontpage discussion list
     */
    function initHomePage() {
        const container = document.getElementById('discussions-container');
        const sentinel = document.getElementById('endless-scroll-sentinel');
        if (!container || !sentinel) return;

        // Get config from global
        const config = window.SnakkConfig;
        if (!config || !config.discussions) return;

        nextCursor = config.discussions.nextCursor || '';
        hasMore = config.discussions.hasMoreItems || false;

        // Count initial discussions
        initialDiscussionCount = container.querySelectorAll('.topic-item-wrapper').length;

        // Disconnect previous observer if it exists
        if (homeScrollObserver) {
            homeScrollObserver.disconnect();
        }

        // Initialize observer for endless scroll
        homeScrollObserver = new IntersectionObserver((entries) => {
            if (entries[0].isIntersecting && !loadMoreRequest && hasMore) {
                loadMore();
            }
        }, {
            rootMargin: '200px' // Start loading before reaching the sentinel
        });

        homeScrollObserver.observe(sentinel);

        // Set up event delegation for preview buttons (only once)
        if (!previewHandlersAttached) {
            attachPreviewHandlers(container);
            previewHandlersAttached = true;
        }

        // Initialize scroll-to-top button
        initScrollToTop();
    }

    /**
     * Attach preview button handlers using event delegation
     */
    function attachPreviewHandlers(container) {

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
                const config = window.SnakkConfig;
                const response = await fetch(`${config.apiBaseUrl}/discussions/${discussionId}/preview`);
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

        // Use event delegation for dynamically added preview buttons
        container.addEventListener('click', function(e) {
            const button = e.target.closest('.preview-btn');
            if (!button) return;

            e.preventDefault();
            const discussionId = button.dataset.discussionId;
            const wrapper = button.closest('.topic-item-wrapper');
            const previewDiv = wrapper.nextElementSibling;

            if (previewDiv && previewDiv.classList.contains('discussion-preview')) {
                togglePreview(button, previewDiv, discussionId);
            }
        });
    }

    /**
     * Create a discussion list item element
     */
    function createDiscussionElement(discussion) {
        const config = window.SnakkConfig;
        const baseUrl = `${config.communityPrefix}/h/${discussion.hub.slug}/${discussion.space.slug}/${discussion.slug}~${discussion.publicId}`;
        const unreadUrl = `${baseUrl}?gotoUnread=true`;
        const spaceAvatarUrl = `${config.apiBaseUrl}/avatars/space/${discussion.space.publicId}.svg`;
        const relativeTime = formatRelativeTime(discussion.lastActivityAt || discussion.createdAt);
        const badges = formatDiscussionBadges(discussion);

        let communityLink = '';
        if (config.showCommunityInDiscussionList && discussion.community) {
            communityLink = `
                <a href="/c/${discussion.community.slug}"
                   class="topic-meta-link"
                   data-popup-type="community"
                   data-popup-id="${discussion.community.publicId}"
                   data-popup-name="${escapeHtml(discussion.community.name)}">${escapeHtml(discussion.community.name)}</a>
                <span class="topic-meta-separator">/</span>
            `;
        }

        const html = `
            <div class="topic-item-wrapper">
                <img src="${spaceAvatarUrl}"
                     alt="${escapeHtml(discussion.space.name)}"
                     loading="lazy"
                     class="w-10 h-10 rounded-full flex-shrink-0 mr-2 hidden sm:block" />

                <div class="topic-item">
                    <div class="topic-content">
                        <div class="topic-title">
                            <a href="${baseUrl}" class="topic-title-link">${escapeHtml(discussion.title)}</a>${badges}
                        </div>
                        <div class="topic-meta">
                            ${communityLink}
                            <a href="${config.communityPrefix}/h/${discussion.hub.slug}"
                               class="topic-meta-link"
                               data-popup-type="hub"
                               data-popup-id="${discussion.hub.publicId}"
                               data-popup-name="${escapeHtml(discussion.hub.name)}">${escapeHtml(discussion.hub.name)}</a>
                            <span class="topic-meta-separator">/</span>
                            <a href="${config.communityPrefix}/h/${discussion.hub.slug}/${discussion.space.slug}"
                               class="topic-meta-link"
                               data-popup-type="space"
                               data-popup-id="${discussion.space.publicId}"
                               data-popup-name="${escapeHtml(discussion.space.name)}">${escapeHtml(discussion.space.name)}</a>
                            <span class="topic-meta-separator">·</span>
                            <span>${relativeTime}</span>
                        </div>
                    </div>
                    <div class="topic-stats hidden sm:flex">
                        <div class="topic-stat">
                            <div class="topic-stat-icon">
                                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <path d="M14 9V5a3 3 0 0 0-3-3l-4 9v11h11.28a2 2 0 0 0 2-1.7l1.38-9a2 2 0 0 0-2-2.3zM7 22H4a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2h3"></path>
                                </svg>
                            </div>
                            <div class="topic-stat-value">${discussion.reactionCount || 0}</div>
                        </div>
                        <div class="topic-stat">
                            <div class="topic-stat-icon">
                                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path>
                                </svg>
                            </div>
                            <div class="topic-stat-value">${discussion.postCount || 0}</div>
                        </div>
                    </div>
                </div>
                <button class="preview-btn"
                        data-discussion-id="${discussion.publicId}"
                        title="Preview first post"
                        type="button">
                    <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <polyline points="6 9 12 15 18 9"></polyline>
                    </svg>
                </button>
                <a href="${unreadUrl}" class="topic-latest-link" title="Jump to first unread post">
                    <svg class="chevron-right" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <polyline points="6 9 12 15 18 9"></polyline>
                    </svg>
                </a>
            </div>
            <div class="discussion-preview hidden" data-discussion-id="${discussion.publicId}">
                <div class="preview-content"></div>
            </div>
        `;

        const template = document.createElement('template');
        template.innerHTML = html.trim();
        return template.content;
    }

    /**
     * Initialize scroll-to-top button
     */
    function initScrollToTop() {
        const scrollWrapper = document.getElementById('scroll-to-top-wrapper');
        scrollToTopBtn = document.getElementById('scroll-to-top-btn');
        scrollCounter = document.getElementById('scroll-counter');

        if (!scrollWrapper || !scrollToTopBtn) return;

        // Update counter on initial load
        updateScrollCounter();

        // Show/hide button based on scroll position
        let scrollTimeout;
        window.addEventListener('scroll', function() {
            if (scrollTimeout) {
                window.cancelAnimationFrame(scrollTimeout);
            }
            scrollTimeout = window.requestAnimationFrame(function() {
                handleScrollPosition();
            });
        }, { passive: true });

        // Scroll to top on click
        scrollToTopBtn.addEventListener('click', function() {
            window.scrollTo({
                top: 0,
                behavior: 'smooth'
            });
        });

        // Initial check
        handleScrollPosition();
    }

    /**
     * Handle scroll position and show/hide button
     */
    function handleScrollPosition() {
        const scrollWrapper = document.getElementById('scroll-to-top-wrapper');
        if (!scrollWrapper) return;

        const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
        const container = document.getElementById('discussions-container');

        if (!container) return;

        const totalDiscussions = container.querySelectorAll('.topic-item-wrapper').length;

        // Show button after scrolling down 800px OR after loading more discussions
        const shouldShow = scrollTop > 800 || totalDiscussions > initialDiscussionCount;

        if (shouldShow) {
            scrollWrapper.classList.remove('hidden');
        } else {
            scrollWrapper.classList.add('hidden');
        }
    }

    /**
     * Update the scroll counter badge
     */
    function updateScrollCounter() {
        if (!scrollCounter) return;

        const container = document.getElementById('discussions-container');
        if (!container) return;

        const totalDiscussions = container.querySelectorAll('.topic-item-wrapper').length;
        scrollCounter.textContent = totalDiscussions;
    }

    /**
     * Load more discussions from the API
     */
    async function loadMore() {
        // Prevent overlapping requests
        if (loadMoreRequest || !hasMore) return;

        const config = window.SnakkConfig;
        const container = document.getElementById('discussions-container');
        const sentinel = document.getElementById('endless-scroll-sentinel');
        const loadingIndicator = document.getElementById('loading-indicator');
        const endOfList = document.getElementById('end-of-list');

        if (!container || !sentinel) return;

        loadingIndicator?.classList.remove('hidden');

        try {
            let url = `/bff/discussions/recent?offset=0&pageSize=${config.discussions.pageSize}`;
            if (config.discussions.communityId) {
                url += `&communityId=${config.discussions.communityId}`;
            }
            if (nextCursor) {
                url += `&cursor=${encodeURIComponent(nextCursor)}`;
            }

            loadMoreRequest = fetch(url, { credentials: 'include' });
            const response = await loadMoreRequest;

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const data = await response.json();

            if (data.items && data.items.length > 0) {
                data.items.forEach(discussion => {
                    const element = createDiscussionElement(discussion);
                    container.appendChild(element);
                });

                nextCursor = data.nextCursor || '';
                hasMore = data.hasMoreItems && !!nextCursor;

                // Update counter after loading more
                updateScrollCounter();
                handleScrollPosition();
            } else {
                hasMore = false;
                nextCursor = '';
            }

            if (!hasMore) {
                sentinel.classList.add('hidden');
                endOfList?.classList.remove('hidden');
            }
        } catch (error) {
            console.error('Failed to load more discussions:', error);
            // Show error message to user
            const errorDiv = document.createElement('div');
            errorDiv.className = 'alert alert-error my-4';
            errorDiv.innerHTML = `
                <svg xmlns="http://www.w3.org/2000/svg" class="stroke-current shrink-0 h-6 w-6" fill="none" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <span>Failed to load more discussions. Please refresh the page.</span>
            `;
            container.appendChild(errorDiv);
            hasMore = false;
        } finally {
            loadMoreRequest = null;
            loadingIndicator?.classList.add('hidden');
        }
    }

    // Run on initial page load
    document.addEventListener('DOMContentLoaded', initHomePage);

    // Run after HTMX content swap (for SPA-like navigation)
    document.body.addEventListener('htmx:load', function(evt) {
        if (document.getElementById('discussions-container')) {
            initHomePage();
        }
    });
})();
