// Space Detail Page (follow buttons + endless scroll discussions)

interface SpaceDiscussion {
    slug: string;
    publicId: string;
    title: string;
    createdAt: string;
    lastActivityAt?: string;
    reactionCount: number;
    postCount: number;
}

(function() {
    'use strict';

    let currentFollowLevel: string | null = null;
    let isFollowing = false;

    // Endless scroll state
    let currentOffset = 0;
    let hasMoreItems = false;
    let spaceScrollObserver: IntersectionObserver | null = null;
    let loadMoreRequest: Promise<Response> | null = null;

    /**
     * Initialize space page functionality
     */
    function initSpacePage(): void {
        const config = window.SnakkConfig;
        const spaceConfig = config?.space;
        if (!config || !spaceConfig) return;

        currentOffset = spaceConfig.initialDiscussionsCount || 0;
        hasMoreItems = spaceConfig.hasMoreItems || false;

        loadFollowStatus();

        if (spaceConfig.preferEndlessScroll) {
            initEndlessScroll();
        }

        // Initialize sticky sidebar
        initStickySidebar();

        // Initialize discussion previews
        initDiscussionPreviews();
    }

    /**
     * Load user's follow status for this space
     */
    async function loadFollowStatus(): Promise<void> {
        const config = window.SnakkConfig;
        const spaceConfig = config?.space;
        if (!spaceConfig) return;
        const spaceId = spaceConfig.publicId;

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
    async function toggleFollowSpace(): Promise<void> {
        const config = window.SnakkConfig;
        const spaceConfig = config?.space;
        if (!spaceConfig) return;
        const spaceId = spaceConfig.publicId;

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
    async function setFollowLevel(level: string): Promise<void> {
        if (!isFollowing) return;

        const config = window.SnakkConfig;
        const spaceConfig = config?.space;
        if (!spaceConfig) return;
        const spaceId = spaceConfig.publicId;

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
    function updateFollowUI(): void {
        const toggleBtn = document.getElementById('follow-toggle-btn');
        const followText = document.getElementById('follow-text');
        const followIcon = document.getElementById('follow-icon');
        const levelToggle = document.getElementById('level-toggle');
        const discussionsBtn = document.getElementById('level-discussions-btn');
        const postsBtn = document.getElementById('level-posts-btn');

        if (!toggleBtn || !followText || !followIcon || !levelToggle || !discussionsBtn || !postsBtn) return;

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
    function initEndlessScroll(): void {
        const sentinel = document.getElementById('scroll-sentinel');
        if (!sentinel) return;

        // Disconnect previous observer if it exists
        if (spaceScrollObserver) {
            spaceScrollObserver.disconnect();
        }

        spaceScrollObserver = new IntersectionObserver((entries) => {
            const entry = entries[0];
            if (entry && entry.isIntersecting && hasMoreItems && !loadMoreRequest) {
                loadMoreDiscussions();
            }
        }, { rootMargin: '100px' });

        spaceScrollObserver.observe(sentinel);
    }

    /**
     * Load more discussions from the API
     */
    async function loadMoreDiscussions(): Promise<void> {
        // Prevent overlapping requests
        if (loadMoreRequest || !hasMoreItems) return;

        const config = window.SnakkConfig;
        const loadingIndicator = document.getElementById('loading-indicator');
        const endMessage = document.getElementById('end-message');
        const container = document.getElementById('discussions-container');

        if (!container) return;

        loadingIndicator?.classList.remove('hidden');

        try {
            const spaceConfig = config?.space;
            if (!spaceConfig) return;
            const spaceId = spaceConfig.publicId;
            const pageSize = spaceConfig.pageSize || 20;

            loadMoreRequest = fetch(
                `/bff/spaces/${spaceId}/discussions?offset=${currentOffset}&pageSize=${pageSize}`,
                { credentials: 'include' }
            );
            const response = await loadMoreRequest;

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const data = await response.json() as { items?: SpaceDiscussion[]; hasMoreItems: boolean };

            if (data.items && data.items.length > 0) {
                data.items.forEach((discussion: SpaceDiscussion) => {
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
    function createDiscussionElement(d: SpaceDiscussion): HTMLDivElement {
        // Extract utility functions from SnakkUtils
        const { formatRelativeTime, formatDiscussionBadges, escapeHtml } = window.SnakkUtils || {};

        const config = window.SnakkConfig;
        const spaceConfig = config?.space;
        if (!spaceConfig) {
            const wrapper = document.createElement('div');
            wrapper.className = 'topic-item-wrapper';
            return wrapper;
        }
        const hubSlug = spaceConfig.hubSlug;
        const spaceSlug = spaceConfig.slug;
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
    /**
     * Sticky sidebar functionality (desktop only)
     */
    function initStickySidebar(): void {
        // Only run on desktop
        if (window.innerWidth < 1024) return;

        const sidebar = document.getElementById('sidebar');
        const nav = document.querySelector('nav.navbar') as HTMLElement | null;

        if (!sidebar || !nav) return;

        let navHeight = 0;
        let sidebarOriginalTop = 0;
        let isSticky = false;
        let ticking = false;

        function updateDimensions() {
            if (!nav || !sidebar) return;
            navHeight = nav.offsetHeight;
            const rect = sidebar.getBoundingClientRect();
            const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
            sidebarOriginalTop = rect.top + scrollTop;

            // Update sidebar max-height
            sidebar.style.maxHeight = `calc(100vh - ${navHeight}px)`;
        }

        function handleScroll() {
            if (!sidebar) return;
            if (!ticking) {
                requestAnimationFrame(() => {
                    if (!sidebar) return;
                    const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
                    const triggerPoint = sidebarOriginalTop - navHeight;

                    if (scrollTop >= triggerPoint && !isSticky) {
                        sidebar.classList.add('sidebar-sticky');
                        sidebar.style.top = `${navHeight}px`;
                        isSticky = true;
                    } else if (scrollTop < triggerPoint && isSticky) {
                        sidebar.classList.remove('sidebar-sticky');
                        sidebar.style.top = '';
                        isSticky = false;
                    }

                    ticking = false;
                });
                ticking = true;
            }
        }

        function handleResize() {
            if (!sidebar) return;
            // Reset on mobile
            if (window.innerWidth < 1024) {
                sidebar.classList.remove('sidebar-sticky');
                sidebar.style.top = '';
                sidebar.style.maxHeight = '';
                isSticky = false;
                return;
            }

            updateDimensions();
            handleScroll();
        }

        // Initial setup
        updateDimensions();
        handleScroll();

        // Event listeners
        window.addEventListener('scroll', handleScroll, { passive: true });
        window.addEventListener('resize', handleResize);
    }

    /**
     * Discussion Preview Feature
     */
    function initDiscussionPreviews(): void {
        const previewCache = new Map<string, string>();

        function truncateText(text: string, maxLength: number): string {
            if (text.length <= maxLength) return text;

            // Find the last space before maxLength
            let truncated = text.substring(0, maxLength);
            const lastSpace = truncated.lastIndexOf(' ');

            if (lastSpace > 0) {
                truncated = truncated.substring(0, lastSpace);
            }

            return truncated + '...';
        }

        async function fetchPreview(discussionId: string): Promise<string | null> {
            if (previewCache.has(discussionId)) {
                const cached = previewCache.get(discussionId);
                return cached !== undefined ? cached : null;
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
                // Hide preview
                previewDiv.classList.add('hidden');
                button.classList.remove('active');
            } else {
                // Show preview
                const previewContent = previewDiv.querySelector('.preview-content') as HTMLElement | null;
                if (!previewContent) return;

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
        document.querySelectorAll('.preview-btn').forEach((button) => {
            button.addEventListener('click', function(this: HTMLElement, e: Event) {
                e.preventDefault();
                const discussionId = this.dataset.discussionId;
                if (!discussionId) return;
                const wrapper = this.closest('.topic-item-wrapper');
                const previewDiv = wrapper?.nextElementSibling as HTMLElement | null;

                if (previewDiv && previewDiv.classList.contains('discussion-preview')) {
                    togglePreview(this, previewDiv, discussionId);
                }
            });
        });
    }

    // ===== Event Delegation =====
    document.addEventListener('click', async (e: MouseEvent) => {
        const target = e.target as HTMLElement | null;
        if (!target) return;

        const action = target.closest('[data-action]') as HTMLElement | null;
        if (!action || !action.dataset.action) return;

        const actionName = action.dataset.action;

        switch (actionName) {
            case 'toggle-follow-space':
                e.preventDefault();
                await toggleFollowSpace();
                break;

            case 'set-follow-level':
                e.preventDefault();
                if (action.dataset.level) {
                    await setFollowLevel(action.dataset.level);
                }
                break;
        }
    });

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
    document.body.addEventListener('htmx:load', function() {
        // Only initialize if this is the space page content AND not already initialized
        if (document.getElementById('discussions-container') && !isSpacePageInitialized) {
            isSpacePageInitialized = true;
            initSpacePage();
        }
    });
})();
