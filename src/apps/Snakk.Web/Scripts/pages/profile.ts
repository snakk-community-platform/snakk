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
    const escapeHtml = window.SnakkUtils?.escapeHtml || function(text: string | null | undefined): string {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    };

    const sanitizeHtml = window.SnakkUtils?.sanitizeHtml || function(html: string): string {
        if (!html) return '';
        const parser = new DOMParser();
        const doc = parser.parseFromString(html, 'text/html');
        doc.querySelectorAll('script,iframe,object,embed,form,base,meta,link,style').forEach(el => el.remove());
        doc.body.querySelectorAll('*').forEach(el => {
            Array.from(el.attributes).forEach(attr => {
                if (attr.name.startsWith('on')) el.removeAttribute(attr.name);
            });
            ['href', 'src', 'action', 'formaction'].forEach(a => {
                const v = el.getAttribute(a);
                if (v && v.trim().toLowerCase().startsWith('javascript:')) el.removeAttribute(a);
            });
        });
        return doc.body.innerHTML;
    };

    const sanitizeUrl = window.SnakkUtils?.sanitizeUrl || function(url: string): string {
        if (!url) return '#';
        const trimmed = url.trim().toLowerCase();
        if (trimmed.startsWith('javascript:') || trimmed.startsWith('data:')) return '#';
        return url;
    };


    const formatRelativeTime = window.SnakkUtils?.formatRelativeTime || function(dateString: string): string {
        const date = new Date(dateString);
        const now = new Date();
        const seconds = Math.floor((now.getTime() - date.getTime()) / 1000);

        if (seconds < 60) return 'just now';
        if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
        if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
        if (seconds < 604800) return `${Math.floor(seconds / 86400)}d ago`;

        const tz = (window as any).snakkTimezone || 'UTC';
        try {
            return date.toLocaleDateString('en-US', { timeZone: tz, month: 'short', day: 'numeric', year: 'numeric' });
        } catch {
            return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
        }
    };

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
                const data = await response.json();

                if (!data.items || data.items.length === 0) {
                    container.innerHTML = `
                        <div class="text-center py-8 text-muted">
                            <p>No posts yet</p>
                        </div>
                    `;
                    return;
                }

                container.innerHTML = `<div class="topic-list">${data.items.map((p: any) => `
                    <div class="topic-item-wrapper">
                        <div class="topic-item">
                            <div class="topic-content">
                                <div class="topic-title">
                                    <a href="${sanitizeUrl(p.url)}" class="topic-title-link">${escapeHtml(p.discussionTitle)}</a>
                                </div>
                                <div class="topic-meta">
                                    <span class="topic-meta-link">${escapeHtml(p.hubName)}</span>
                                    <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 18 15 12 9 6"></polyline></svg>
                                    <span class="topic-meta-link">${escapeHtml(p.spaceName)}</span>
                                    <span class="topic-meta-separator">&middot;</span>
                                    <span>${formatRelativeTime(p.createdAt)}</span>
                                </div>
                                <div class="prose prose-sm max-w-none mt-1 text-sm text-base-content/70 fp-post-preview">
                                    ${sanitizeHtml(p.contentPreview)}
                                </div>
                            </div>
                            <a href="${sanitizeUrl(p.url)}" class="topic-latest-link" title="Go to discussion">
                                <svg class="chevron-right" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <polyline points="6 9 12 15 18 9"></polyline>
                                </svg>
                            </a>
                        </div>
                    </div>
                `).join('')}</div>`;
            } catch (error) {
                console.error('Error loading posts:', error);
                container.innerHTML = '<div class="text-center py-8 text-error">Failed to load posts</div>';
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
                container.innerHTML = '<div class="text-center py-8 text-error">Failed to load activity chart</div>';
            }
        }

        function renderActivityChart(container: HTMLElement, data: ActivityDataPoint[], days: number, maxHeight: number = 150): void {
            if (!data || data.length === 0) {
                container.innerHTML = `
                    <div class="text-center py-12">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-16 w-16 mx-auto text-muted mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                        </svg>
                        <h3 class="font-semibold mb-2">No activity yet</h3>
                        <p class="text-sm text-muted">Activity will appear here once this user starts contributing</p>
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
                    <div class="activity-chart-bar-wrapper">
                        <div class="activity-chart-bar-container" style="height: ${maxHeight}px;">
                            <div class="activity-chart-bar"
                                 style="height: ${day.total === 0 ? '4px' : heightPercent + '%'}; ${day.total === 0 ? 'min-height: 4px;' : ''}"
                                 title="${day.total} contribution${day.total !== 1 ? 's' : ''}\\n${day.discussions} discussion${day.discussions !== 1 ? 's' : ''}\\n${day.posts} post${day.posts !== 1 ? 's' : ''}\\n${dateLabel}">
                                ${day.discussions > 0 ? `<div class="activity-chart-bar-segment-primary" style="height: ${discussionsPercent}%;"></div>` : ''}
                                ${day.posts > 0 ? `<div class="activity-chart-bar-segment-secondary" style="height: ${postsPercent}%;"></div>` : ''}
                                ${day.total === 0 ? '<div class="activity-chart-bar-zero"></div>' : ''}
                            </div>
                        </div>
                    </div>
                `;
            }).join('');

            const totalDiscussions = data.reduce((sum: number, d: ActivityDataPoint) => sum + d.discussions, 0);
            const totalPosts = data.reduce((sum: number, d: ActivityDataPoint) => sum + d.posts, 0);
            const totalActivity = totalDiscussions + totalPosts;

            container.innerHTML = `
                <div class="space-y-4">
                    <div class="activity-chart-wrapper" style="height: ${maxHeight + 40}px;">
                        ${barsHtml}
                    </div>
                    <div class="activity-chart-legend">
                        <div class="activity-chart-legend-item">
                            <div class="activity-chart-legend-color activity-chart-legend-color-primary"></div>
                            <span>${totalDiscussions} discussions</span>
                        </div>
                        <div class="activity-chart-legend-item">
                            <div class="activity-chart-legend-color activity-chart-legend-color-secondary"></div>
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
                const data = await response.json();

                if (!data.entries || data.entries.length === 0) return;

                list.innerHTML = data.entries.map((e: any) => `
                    <div class="flex items-center justify-between text-sm py-1.5">
                        <div class="min-w-0">
                            <span class="text-base-content/50 line-through">${escapeHtml(e.previousName)}</span>
                            <span class="text-base-content/40 mx-1">&rarr;</span>
                            <span class="font-medium">${escapeHtml(e.newName)}</span>
                        </div>
                        <span class="text-xs text-base-content/40 shrink-0 ml-2">${formatRelativeTime(e.changedAt)}</span>
                    </div>
                `).join('');

                section.classList.remove('hidden');
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
                const authData = await authResponse.json();

                if (!authData.isAuthenticated) {
                    container.innerHTML = ''; // No actions for anonymous users
                    return;
                }

                if (authData.publicId === userId) {
                    // Viewing own profile
                    container.innerHTML = `
                        <a href="/settings" class="btn btn-outline btn-sm">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                            </svg>
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
                const followData = await followResponse.json();

                container.innerHTML = `
                    <button data-action="toggle-follow-user"
                            data-user-id="${userId}"
                            class="btn btn-outline btn-sm"
                            id="follow-btn">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="${followData.isFollowing ? 'M5 13l4 4L19 7' : 'M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z'}" />
                        </svg>
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
        const tabs = document.querySelectorAll<HTMLElement>('.fp-profile-tab');
        tabs.forEach(tab => {
            tab.addEventListener('click', () => {
                const target = tab.dataset.tab;
                tabs.forEach(t => {
                    t.classList.toggle('active', t === tab);
                    t.setAttribute('aria-selected', t === tab ? 'true' : 'false');
                });
                document.querySelectorAll<HTMLElement>('.fp-profile-tab-panel').forEach(p => {
                    p.hidden = p.id !== 'tab-' + target;
                });
            });
        });
    }

    // Self-initialize
    initializeProfile();
    initProfileTabs();
})();
