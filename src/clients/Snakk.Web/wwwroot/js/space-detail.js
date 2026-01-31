// Space Detail Page (follow buttons + endless scroll discussions)

(function() {
    'use strict';

    let currentFollowLevel = null;
    let isFollowing = false;

    // Endless scroll state
    let currentOffset = 0;
    let hasMoreItems = false;
    let spaceScrollObserver = null;
    let loadMoreRequest = null;

    /**
     * Initialize space page functionality
     */
    function initSpacePage() {
        const config = window.SnakkConfig;
        if (!config || !config.space) return;

        currentOffset = config.space.initialDiscussionsCount || 0;
        hasMoreItems = config.space.hasMoreItems || false;

        loadFollowStatus();

        if (config.space.preferEndlessScroll) {
            initEndlessScroll();
        }
    }

    /**
     * Load user's follow status for this space
     */
    async function loadFollowStatus() {
        const config = window.SnakkConfig;
        const spaceId = config.space.publicId;

        try {
            const response = await fetch(`/bff/spaces/${spaceId}/follow-status`, {
                credentials: 'include'
            });
            const result = await response.json();
            isFollowing = result.isFollowing;
            currentFollowLevel = result.level;
            updateFollowUI();
        } catch (err) {
            console.error('Error loading follow status:', err);
        }
    }

    /**
     * Toggle follow status for this space
     */
    async function toggleFollowSpace() {
        const config = window.SnakkConfig;
        const spaceId = config.space.publicId;

        try {
            const level = currentFollowLevel || 'DiscussionsOnly';
            const response = await fetch(`/bff/spaces/${spaceId}/follow?level=${level}`, {
                method: 'POST',
                credentials: 'include'
            });

            if (!response.ok) {
                console.error('Failed to toggle follow');
                return;
            }

            const result = await response.json();
            isFollowing = result.isFollowing;
            if (isFollowing) {
                currentFollowLevel = result.level || 'DiscussionsOnly';
            }
            updateFollowUI();
        } catch (err) {
            console.error('Error toggling follow:', err);
        }
    }

    /**
     * Set the follow level (DiscussionsOnly or DiscussionsAndPosts)
     */
    async function setFollowLevel(level) {
        if (!isFollowing) return;

        const config = window.SnakkConfig;
        const spaceId = config.space.publicId;

        try {
            const response = await fetch(`/bff/spaces/${spaceId}/follow-level?level=${level}`, {
                method: 'PUT',
                credentials: 'include'
            });

            if (!response.ok) {
                console.error('Failed to update follow level');
                return;
            }

            const result = await response.json();
            currentFollowLevel = result.level;
            updateFollowUI();
        } catch (err) {
            console.error('Error updating follow level:', err);
        }
    }

    /**
     * Update the follow button UI based on current state
     */
    function updateFollowUI() {
        const toggleBtn = document.getElementById('follow-toggle-btn');
        const followText = document.getElementById('follow-text');
        const followIcon = document.getElementById('follow-icon');
        const levelToggle = document.getElementById('level-toggle');
        const discussionsBtn = document.getElementById('level-discussions-btn');
        const postsBtn = document.getElementById('level-posts-btn');

        if (isFollowing) {
            // Following state
            toggleBtn.classList.add('btn-primary');
            toggleBtn.classList.remove('btn-ghost');
            toggleBtn.classList.remove('rounded-r-none', 'border-r-0');
            toggleBtn.classList.add('rounded-l-lg', 'rounded-r-none');
            followText.textContent = 'Following';
            followIcon.innerHTML = '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />';

            // Show level toggle
            levelToggle.classList.remove('hidden');

            // Update level buttons
            if (currentFollowLevel === 'DiscussionsAndPosts') {
                discussionsBtn.classList.remove('btn-primary');
                discussionsBtn.classList.add('btn-ghost');
                postsBtn.classList.add('btn-primary');
                postsBtn.classList.remove('btn-ghost');
            } else {
                discussionsBtn.classList.add('btn-primary');
                discussionsBtn.classList.remove('btn-ghost');
                postsBtn.classList.remove('btn-primary');
                postsBtn.classList.add('btn-ghost');
            }
        } else {
            // Not following state
            toggleBtn.classList.remove('btn-primary');
            toggleBtn.classList.add('btn-ghost');
            toggleBtn.classList.add('rounded-lg');
            toggleBtn.classList.remove('rounded-l-lg', 'rounded-r-none');
            followText.textContent = 'Follow';
            followIcon.innerHTML = '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />';

            // Hide level toggle
            levelToggle.classList.add('hidden');
        }
    }

    /**
     * Initialize endless scroll for discussions
     */
    function initEndlessScroll() {
        const sentinel = document.getElementById('scroll-sentinel');
        if (!sentinel) return;

        // Disconnect previous observer if it exists
        if (spaceScrollObserver) {
            spaceScrollObserver.disconnect();
        }

        spaceScrollObserver = new IntersectionObserver((entries) => {
            if (entries[0].isIntersecting && hasMoreItems && !loadMoreRequest) {
                loadMoreDiscussions();
            }
        }, { rootMargin: '100px' });

        spaceScrollObserver.observe(sentinel);
    }

    /**
     * Load more discussions from the API
     */
    async function loadMoreDiscussions() {
        // Prevent overlapping requests
        if (loadMoreRequest || !hasMoreItems) return;

        const config = window.SnakkConfig;
        const spaceId = config.space.publicId;
        const pageSize = config.space.pageSize || 20;

        const loadingIndicator = document.getElementById('loading-indicator');
        const endMessage = document.getElementById('end-message');
        const container = document.getElementById('discussions-container');

        if (!container) return;

        loadingIndicator?.classList.remove('hidden');

        try {
            loadMoreRequest = fetch(
                `/bff/spaces/${spaceId}/discussions?offset=${currentOffset}&pageSize=${pageSize}`,
                { credentials: 'include' }
            );
            const response = await loadMoreRequest;

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const data = await response.json();

            if (data.items && data.items.length > 0) {
                data.items.forEach(discussion => {
                    container.appendChild(createDiscussionElement(discussion));
                });
                currentOffset += data.items.length;
            }

            hasMoreItems = data.hasMoreItems;

            if (!hasMoreItems) {
                endMessage?.classList.remove('hidden');
            }
        } catch (err) {
            console.error('Failed to load more discussions:', err);
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
            hasMoreItems = false;
        } finally {
            loadMoreRequest = null;
            loadingIndicator?.classList.add('hidden');
        }
    }

    /**
     * Create a discussion list item element
     */
    function createDiscussionElement(d) {
        const config = window.SnakkConfig;
        const hubSlug = config.space.hubSlug;
        const spaceSlug = config.space.slug;
        const baseUrl = `${config.communityPrefix}/h/${hubSlug}/${spaceSlug}/${d.slug}~${d.publicId}`;
        const unreadUrl = `${baseUrl}?gotoUnread=true`;

        const wrapper = document.createElement('div');
        wrapper.className = 'topic-item-wrapper';

        const link = document.createElement('a');
        link.href = baseUrl;
        link.className = 'topic-item';

        const createdDate = new Date(d.createdAt).toLocaleDateString('en-US', {
            month: 'short',
            day: 'numeric',
            year: 'numeric'
        });

        const badges = formatDiscussionBadges(d);

        let lastActivity = '';
        if (d.lastActivityAt) {
            lastActivity = `<span class="topic-meta-separator">·</span><span>Last activity ${formatRelativeTime(d.lastActivityAt)}</span>`;
        }

        link.innerHTML = `
            <div class="topic-content">
                <div class="topic-title">${escapeHtml(d.title)}${badges}</div>
                <div class="topic-meta">
                    <span>${createdDate}</span>
                    ${lastActivity}
                </div>
            </div>
            <div class="topic-stats hidden sm:flex">
                <div class="topic-stat">
                    <div class="topic-stat-value">${d.reactionCount || 0}</div>
                    <div class="topic-stat-label">Reactions</div>
                </div>
                <div class="topic-stat">
                    <div class="topic-stat-value">${d.postCount || 0}</div>
                    <div class="topic-stat-label">Replies</div>
                </div>
            </div>
        `;

        const chevron = document.createElement('a');
        chevron.href = unreadUrl;
        chevron.className = 'topic-latest-link';
        chevron.title = 'Jump to first unread post';
        chevron.onclick = (e) => e.stopPropagation();
        chevron.innerHTML = `
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="9 18 15 12 9 6"></polyline>
            </svg>
        `;

        wrapper.appendChild(link);
        wrapper.appendChild(chevron);
        return wrapper;
    }

    // Expose functions to global scope for button onclick handlers
    window.toggleFollowSpace = toggleFollowSpace;
    window.setFollowLevel = setFollowLevel;

    // Track if page has been initialized to prevent duplicate calls
    let isSpacePageInitialized = false;

    // Run on initial page load
    document.addEventListener('DOMContentLoaded', function() {
        if (!isSpacePageInitialized) {
            isSpacePageInitialized = true;
            initSpacePage();
        }
    });

    // Run after HTMX content swap (for SPA-like navigation)
    document.body.addEventListener('htmx:load', function(evt) {
        // Only initialize if this is the space page content AND not already initialized
        if (document.getElementById('discussions-container') && !isSpacePageInitialized) {
            isSpacePageInitialized = true;
            initSpacePage();
        }
    });
})();
