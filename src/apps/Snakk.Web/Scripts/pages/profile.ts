/**
 * User Profile Page
 * Handles profile display, activity charts, and user interactions
 */

interface UserStats {
    totalActivity: number;
    daysSinceJoined: number;
    discussionCount: number;
    postCount: number;
}

interface ActivityDataPoint {
    date: string;
    discussions: number;
    posts: number;
    total: number;
    isWeek?: boolean;
}

(function() {
    'use strict';

    // Use utilities from utils.js
    const escapeHtml = (text: string): string => (window as any).SnakkUtils.escapeHtml(text);

    const sanitizeHtml = (html: string): string => (window as any).SnakkUtils.sanitizeHtml(html);

    const sanitizeUrl = window.SnakkUtils?.sanitizeUrl || function(url: string): string {
        if (!url) return '#';
        const trimmed = url.trim().toLowerCase();
        if (trimmed.startsWith('javascript:') || trimmed.startsWith('data:')) return '#';
        return url;
    };


    const formatRelativeTime = (dateString: string): string => (window as any).SnakkUtils.formatRelativeTime(dateString);

    // Read config from JSON tag
    const configEl = document.getElementById('profile-page-config');
    if (!configEl) return; // No profile data — nothing to initialize

    const profileConfig: { userId: string } & UserStats = JSON.parse(configEl.textContent || '{}');
    const userId = profileConfig.userId;

    function initializeProfile(): void {
        // Load user stats
        async function loadUserStats(): Promise<void> {
            try {
                const response = await fetch(`/bff/users/${userId}/stats`);
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                const data = await response.json();

                const followerStat = document.getElementById('stat-followers');
                if (followerStat) {
                    followerStat.textContent = data.followerCount || 0;
                }

                const replyStat = document.getElementById('stat-replies');
                if (replyStat && data.replyCount !== undefined) {
                    replyStat.textContent = data.replyCount;
                }
            } catch (error) {
                console.error('Error loading user stats:', error);
                const followerStat = document.getElementById('stat-followers');
                if (followerStat) {
                    followerStat.textContent = '0';
                }
            }
        }

        // Recent posts
        async function loadRecentPosts(limit: number): Promise<void> {
            const container = document.getElementById('recent-posts');
            if (!container) return;

            try {
                const response = await fetch(`/bff/search/posts?authorPublicId=${userId}&pageSize=${limit}`);
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                const data = await response.json();

                if (!data.items || data.items.length === 0) {
                    container.innerHTML = `
                        <div class="sn-text-center sn-py-8 sn-text-muted">
                            <p>No posts yet</p>
                        </div>
                    `;
                    return;
                }

                container.innerHTML = `<div class="sn-card-list">${data.items.map((p: any) => {
                    const postUrl = p.postPublicId ? `${sanitizeUrl(p.url)}#post-${p.postPublicId}` : sanitizeUrl(p.url);
                    const authorHref = p.authorPublicId ? `/u/${encodeURIComponent(p.authorPublicId)}` : '#';
                    const spaceStyle = p.spaceGradientCss ? ` style="--space-grad: ${p.spaceGradientCss}"` : '';
                    const excerpt = p.contentPreview
                        ? `<div class="sn-card-excerpt"><div class="sn-post-preview sn-prose sn-prose-sm">${sanitizeHtml(p.contentPreview)}</div></div>`
                        : '';
                    return `
                    <article class="sn-card">
                        <div class="sn-card-header">
                            <img src="${escapeHtml(p.authorAvatarUrl || '')}" alt="" class="sn-card-avatar" width="20" height="20" loading="lazy" decoding="async" />
                            <a href="${authorHref}" class="sn-card-author">${escapeHtml(p.authorDisplayName || '')}</a>
                            <span class="sn-card-dot">&middot;</span>
                            <time class="sn-card-time">${formatRelativeTime(p.createdAt)}</time>
                            <span class="sn-card-path">
                                <span class="sn-card-path-link">${escapeHtml(p.hubName)}</span>
                                <span class="sn-card-path-sep">&rsaquo;</span>
                                <span class="sn-card-tag sn-card-tag--space"${spaceStyle}>${escapeHtml(p.spaceName)}</span>
                            </span>
                        </div>
                        <div class="sn-card-heading-row">
                            <div class="sn-card-heading">
                                <a href="${postUrl}">${escapeHtml(p.discussionTitle)}</a>
                            </div>
                        </div>
                        ${excerpt}
                    </article>`;
                }).join('')}</div>`;
            } catch (error) {
                console.error('Error loading posts:', error);
                container.innerHTML = '<div class="sn-text-center sn-py-8 sn-text-error">Failed to load posts</div>';
            }
        }

        // Activity Chart — renders in sidebar on lg+, main column on smaller screens
        async function loadActivityChart(days: number): Promise<void> {
            const sidebarChart = document.getElementById('activity-chart-sidebar');
            const mainChart = document.getElementById('activity-chart-main');
            const isLg = window.matchMedia('(min-width: 1024px)').matches;
            const container = isLg ? sidebarChart : mainChart;
            if (!container) return;

            try {
                const response = await fetch(`/bff/users/${userId}/activity-history?days=${days}`);
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                const result = await response.json();

                const data: ActivityDataPoint[] = (result.activities || []).map((a: any) => ({
                    date: a.date,
                    discussions: a.discussionCount ?? 0,
                    posts: a.postCount ?? 0,
                    total: (a.discussionCount ?? 0) + (a.postCount ?? 0)
                }));
                // Measure available height so the chart fills its flex-sized container.
                // Reserve ~40px for the legend row below the bars. Prefer the real
                // measurement whenever the container has been laid out (any positive
                // height) so the chart never inflates a deliberately compact wrapper —
                // 150 only kicks in if the container wasn't measurable at all.
                const measured = container.clientHeight;
                const maxHeight = measured > 0 ? Math.max(measured - 40, 60) : 150;
                renderActivityChart(container, data, days, maxHeight);
            } catch (error) {
                console.error('Error loading activity chart:', error);
                container.innerHTML = '<div class="sn-text-center sn-py-8 sn-text-error">Failed to load activity chart</div>';
            }
        }

        function renderActivityChart(container: HTMLElement, data: ActivityDataPoint[], days: number, maxHeight: number = 150): void {
            if (!data || data.length === 0) {
                container.innerHTML = `
                    <div class="sn-text-center sn-py-12">
                        <span class="sn-icon icon-chart-bar sn-h-16 sn-w-16 sn-mx-auto sn-text-muted sn-mb-4" aria-hidden="true"></span>
                        <h3 class="sn-font-semibold sn-mb-2">No activity yet</h3>
                        <p class="sn-text-sm sn-text-muted">Activity will appear here once this user starts contributing</p>
                    </div>
                `;
                return;
            }

            // Calculate max value for scaling
            const maxValue = Math.max(...data.map((d: ActivityDataPoint) => d.total), 1);

            // Group by week for better visualization if > 30 days
            const shouldGroupByWeek = days > 30;
            let chartData = data;

            if (shouldGroupByWeek) {
                const grouped = [];
                for (let i = 0; i < data.length; i += 7) {
                    const week = data.slice(i, i + 7);
                    if (week.length === 0 || !week[0]) continue;
                    const weekTotal: ActivityDataPoint = {
                        date: week[0].date,
                        discussions: week.reduce((sum: number, d: ActivityDataPoint) => sum + d.discussions, 0),
                        posts: week.reduce((sum: number, d: ActivityDataPoint) => sum + d.posts, 0),
                        total: week.reduce((sum: number, d: ActivityDataPoint) => sum + d.total, 0),
                        isWeek: true
                    };
                    grouped.push(weekTotal);
                }
                chartData = grouped;
            }

            const barsHtml = chartData.map((day: ActivityDataPoint) => {
                const heightPercent = maxValue > 0 ? (day.total / maxValue) * 100 : 0;
                const discussionsPercent = day.total > 0 ? (day.discussions / day.total) * 100 : 0;
                const postsPercent = day.total > 0 ? (day.posts / day.total) * 100 : 0;

                const tz = (window as any).snakkTimezone || 'UTC';
                const dateOpts: Intl.DateTimeFormatOptions = { timeZone: tz, month: 'short', day: 'numeric' };
                const fmtDate = (d: string) => { try { return new Date(d).toLocaleDateString('en-US', dateOpts); } catch { return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric' }); } };
                const dateLabel = shouldGroupByWeek
                    ? `Week of ${fmtDate(day.date)}`
                    : fmtDate(day.date);

                return `
                    <div class="sn-activity-chart-bar-wrapper">
                        <div class="sn-activity-chart-bar-container" style="height: ${maxHeight}px;">
                            <div class="sn-activity-chart-bar"
                                 style="height: ${day.total === 0 ? '4px' : heightPercent + '%'}; ${day.total === 0 ? 'min-height: 4px;' : ''}"
                                 title="${day.total} contribution${day.total !== 1 ? 's' : ''}\\n${day.discussions} discussion${day.discussions !== 1 ? 's' : ''}\\n${day.posts} post${day.posts !== 1 ? 's' : ''}\\n${dateLabel}">
                                ${day.discussions > 0 ? `<div class="sn-activity-chart-bar-segment-primary" style="height: ${discussionsPercent}%;"></div>` : ''}
                                ${day.posts > 0 ? `<div class="sn-activity-chart-bar-segment-secondary" style="height: ${postsPercent}%;"></div>` : ''}
                                ${day.total === 0 ? '<div class="sn-activity-chart-bar-zero"></div>' : ''}
                            </div>
                        </div>
                    </div>
                `;
            }).join('');

            const totalDiscussions = data.reduce((sum: number, d: ActivityDataPoint) => sum + d.discussions, 0);
            const totalPosts = data.reduce((sum: number, d: ActivityDataPoint) => sum + d.posts, 0);
            const totalActivity = totalDiscussions + totalPosts;

            container.innerHTML = `
                <div class="sn-space-y-4">
                    <div class="sn-activity-chart-wrapper" style="height: ${maxHeight + 40}px;">
                        ${barsHtml}
                    </div>
                    <div class="sn-activity-chart-legend">
                        <div class="sn-activity-chart-legend-item">
                            <div class="sn-activity-chart-legend-color sn-activity-chart-legend-color-primary"></div>
                            <span>${totalDiscussions} discussions</span>
                        </div>
                        <div class="sn-activity-chart-legend-item">
                            <div class="sn-activity-chart-legend-color sn-activity-chart-legend-color-secondary"></div>
                            <span>${totalPosts} posts</span>
                        </div>
                        <span class="text-base-content/50">(${totalActivity} total)</span>
                    </div>
                </div>
            `;
        }

        // Name History (own profile only)
        async function loadNameHistory(): Promise<void> {
            const section = document.getElementById('name-history-section');
            const list = document.getElementById('name-history-list');
            if (!section || !list) return;

            try {
                const response = await fetch('/bff/me/display-name-history', { credentials: 'include' });
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                const data = await response.json();

                if (!data.entries || data.entries.length === 0) return;

                list.innerHTML = data.entries.map((e: any) => `
                    <div class="sn-flex sn-items-center sn-justify-between sn-text-sm sn-py-1.5">
                        <div class="sn-min-w-0">
                            <span class="text-base-content/50 sn-line-through">${escapeHtml(e.previousName)}</span>
                            <span class="text-base-content/40 sn-mx-1">&rarr;</span>
                            <span class="sn-font-medium">${escapeHtml(e.newName)}</span>
                        </div>
                        <span class="sn-text-xs text-base-content/40 sn-shrink-0 sn-ml-2">${formatRelativeTime(e.changedAt)}</span>
                    </div>
                `).join('');

                section.classList.remove('sn-hidden');
            } catch (error) {
                console.error('Error loading name history:', error);
            }
        }

        // Profile Actions (Follow/Unfollow)
        async function loadProfileActions(): Promise<void> {
            const container = document.getElementById('profile-actions');
            if (!container) return;

            try {
                // Check if user is authenticated and viewing someone else's profile
                const authResponse = await fetch(`/bff/auth/status`, { credentials: 'include' });
                if (!authResponse.ok) throw new Error(`HTTP ${authResponse.status}`);
                const authData = await authResponse.json();

                if (!authData.isAuthenticated) {
                    container.innerHTML = ''; // No actions for anonymous users
                    return;
                }

                if (authData.publicId === userId) {
                    // Viewing own profile
                    container.innerHTML = `
                        <a href="/my/settings" class="sn-btn sn-btn-outline sn-btn-sm">
                            <span class="icon icon-pencil h-4 w-4" aria-hidden="true"></span>
                            Edit Profile
                        </a>
                    `;
                    loadNameHistory();
                    return;
                }

                // Check follow status
                const followResponse = await fetch(`/bff/users/${userId}/follow-status?currentUserId=${authData.publicId}`, {
                    credentials: 'include'
                });
                if (!followResponse.ok) throw new Error(`HTTP ${followResponse.status}`);
                const followData = await followResponse.json();

                container.innerHTML = `
                    <button data-action="toggle-follow-user"
                            data-user-id="${userId}"
                            class="sn-btn sn-btn-outline sn-btn-sm"
                            id="follow-btn">
                        ${followData.isFollowing ? '<span class="icon icon-check h-4 w-4" aria-hidden="true"></span>' : '<span class="icon icon-user-follow h-4 w-4" aria-hidden="true"></span>'}
                        <span id="follow-btn-text">${followData.isFollowing ? 'Following' : 'Follow'}</span>
                    </button>
                `;
            } catch (error) {
                console.error('Error loading profile actions:', error);
                container.innerHTML = '';
            }
        }

        // Toggle follow/unfollow
        async function toggleFollowUser(targetUserId: string): Promise<void> {
            const btn = document.getElementById('follow-btn') as HTMLButtonElement | null;
            const btnText = document.getElementById('follow-btn-text');
            if (!btn || !btnText) return;

            btn.disabled = true;

            try {
                const response = await fetch(`/bff/users/${targetUserId}/follow`, {
                    method: 'POST',
                    credentials: 'include'
                });

                if (response.ok) {
                    const result = await response.json();
                    btnText.textContent = result.isFollowing ? 'Following' : 'Follow';

                    // Update follower count
                    loadUserStats();
                } else {
                    throw new Error('Failed to toggle follow');
                }
            } catch (error) {
                console.error('Error toggling follow:', error);
                alert('Failed to update follow status');
            } finally {
                btn.disabled = false;
            }
        }

        // Initialize all sections
        loadUserStats();
        loadProfileActions();
        loadActivityChart(365);
        loadRecentPosts(5);

        // Event delegation for profile actions
        document.addEventListener('click', async (e: MouseEvent) => {
            const target = e.target as HTMLElement | null;
            if (!target) return;

            const action = target.closest('[data-action]') as HTMLElement | null;
            if (!action || !action.dataset.action) return;

            const actionName = action.dataset.action;

            switch (actionName) {
                case 'toggle-follow-user':
                    e.preventDefault();
                    if (action.dataset.userId) {
                        await toggleFollowUser(action.dataset.userId);
                    }
                    break;

                case 'load-activity-chart':
                    e.preventDefault();
                    if (action.dataset.days) {
                        await loadActivityChart(parseInt(action.dataset.days, 10));
                    }
                    break;
            }
        });
    }

    // Profile tabs
    function initProfileTabs(): void {
        const tabList = document.querySelector<HTMLElement>('[role="tablist"]');
        if (!tabList) return;
        tabList.addEventListener('click', (e) => {
            const tab = (e.target as HTMLElement).closest<HTMLElement>('[data-tab]');
            if (!tab) return;
            const target = tab.dataset.tab;
            if (!target) return;
            tabList.querySelectorAll<HTMLElement>('[data-tab]').forEach(t => {
                t.classList.toggle('sn-active', t === tab);
                t.setAttribute('aria-selected', t === tab ? 'true' : 'false');
            });
            document.querySelectorAll<HTMLElement>('.sn-profile-tab-panel').forEach(p => {
                p.hidden = p.id !== 'tab-' + target;
            });
            // Update URL: replace or append the tab path segment
            const validTabs = ['discussions', 'top', 'posts'];
            const parts = window.location.pathname.replace(/\/$/, '').split('/');
            if (validTabs.includes(parts[parts.length - 1] ?? '')) {
                parts[parts.length - 1] = target;
            } else {
                parts.push(target);
            }
            history.replaceState(null, '', parts.join('/'));
        });
    }

    // Self-initialize
    initializeProfile();
    initProfileTabs();
})();
