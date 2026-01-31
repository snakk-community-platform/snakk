// Frontpage Discussion List (endless scroll)

(function() {
    'use strict';

    let nextCursor = '';
    let hasMore = false;
    let homeScrollObserver = null;
    let loadMoreRequest = null;

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
                            <div class="topic-stat-value">${discussion.reactionCount || 0}</div>
                            <div class="topic-stat-label">Reactions</div>
                        </div>
                        <div class="topic-stat">
                            <div class="topic-stat-value">${discussion.postCount || 0}</div>
                            <div class="topic-stat-label">Replies</div>
                        </div>
                    </div>
                </div>
                <a href="${unreadUrl}" class="topic-latest-link" title="Jump to first unread post">
                    <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <polyline points="9 18 15 12 9 6"></polyline>
                    </svg>
                </a>
            </div>
        `;

        const template = document.createElement('template');
        template.innerHTML = html.trim();
        return template.content.firstChild;
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
