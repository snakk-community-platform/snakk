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

    /**
     * Initialize profile page with data from server
     */
    function initializeProfile(userId: string, stats: UserStats): void {
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

        // Recent discussions
        async function loadRecentDiscussions(limit: number): Promise<void> {
            const container = document.getElementById('recent-discussions');
            if (!container) return;

            try {
                const response = await fetch(`/bff/search/discussions?authorPublicId=${userId}&pageSize=${limit}`);
                const data = await response.json();

                if (!data.items || data.items.length === 0) {
                    container.innerHTML = `
                        <div class="text-center py-8 text-muted">
                            <p>No discussions yet</p>
                        </div>
                    `;
                    return;
                }

                container.innerHTML = `<div class="topic-list">${data.items.map((d: any) => `
                    <div class="topic-item-wrapper">
                        <div class="topic-item">
                            <div class="topic-content">
                                <div class="topic-title">
                                    <a href="${sanitizeUrl(d.url)}" class="topic-title-link">${escapeHtml(d.title)}</a>
                                </div>
                                <div class="topic-meta">
                                    <span class="font-medium">${escapeHtml(d.hubName)}</span>
                                    <span class="topic-meta-separator">/</span>
                                    <span class="font-medium">${escapeHtml(d.spaceName)}</span>
                                    <span class="topic-meta-separator">&middot;</span>
                                    <span>${formatRelativeTime(d.lastActivityAt || d.createdAt)}</span>
                                </div>
                            </div>
                            <div class="topic-stats hidden sm:flex">
                                <div class="topic-stat">
                                    <div class="topic-stat-icon">
                                        <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                            <path d="M14 9V5a3 3 0 0 0-3-3l-4 9v11h11.28a2 2 0 0 0 2-1.7l1.38-9a2 2 0 0 0-2-2.3zM7 22H4a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2h3"></path>
                                        </svg>
                                    </div>
                                    <div class="topic-stat-value">${d.reactionCount}</div>
                                </div>
                                <div class="topic-stat">
                                    <div class="topic-stat-icon">
                                        <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                            <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path>
                                        </svg>
                                    </div>
                                    <div class="topic-stat-value">${d.postCount}</div>
                                </div>
                            </div>
                            <a href="${sanitizeUrl(d.url)}" class="topic-latest-link" title="Go to discussion">
                                <svg class="chevron-right" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <polyline points="6 9 12 15 18 9"></polyline>
                                </svg>
                            </a>
                        </div>
                    </div>
                `).join('')}</div>`;
            } catch (error) {
                console.error('Error loading discussions:', error);
                container.innerHTML = '<div class="text-center py-8 text-error">Failed to load discussions</div>';
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
                                    <span class="font-medium">${escapeHtml(p.hubName)}</span>
                                    <span class="topic-meta-separator">/</span>
                                    <span class="font-medium">${escapeHtml(p.spaceName)}</span>
                                    <span class="topic-meta-separator">&middot;</span>
                                    <span>${formatRelativeTime(p.createdAt)}</span>
                                </div>
                                <div class="prose prose-sm max-w-none mt-1 text-sm text-base-content/70 line-clamp-2">
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
                renderActivityChart(container, data, days);
            } catch (error) {
                console.error('Error loading activity chart:', error);
                container.innerHTML = '<div class="text-center py-8 text-error">Failed to load activity chart</div>';
            }
        }

        function renderActivityChart(container: HTMLElement, data: ActivityDataPoint[], days: number): void {
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
            const maxHeight = 150; // pixels

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
                        <div class="activity-chart-legend-item">
                            <div class="activity-chart-legend-color bg-accent"></div>
                            <span>${totalActivity} total</span>
                        </div>
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
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
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
                            class="btn ${followData.isFollowing ? 'btn-outline' : 'btn-primary'} btn-sm"
                            id="follow-btn">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
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

                    if (result.isFollowing) {
                        btn.classList.remove('btn-primary');
                        btn.classList.add('btn-outline');
                    } else {
                        btn.classList.remove('btn-outline');
                        btn.classList.add('btn-primary');
                    }

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

        // Generate achievements based on activity
        function loadAchievements(): void {
            const section = document.getElementById('achievements-section');
            const grid = document.getElementById('achievements-grid');
            if (!section || !grid) return;

            const achievements: { name: string; icon: string; color: string; description: string }[] = [];
            const totalActivity = stats.totalActivity;
            const daysSinceJoined = stats.daysSinceJoined;
            const discussionCount = stats.discussionCount;
            const postCount = stats.postCount;

            // Activity level achievements
            if (totalActivity >= 1000) {
                achievements.push({
                    name: 'Power User',
                    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M16.5 18.75h-9m9 0a3 3 0 013 3h-15a3 3 0 013-3m9 0v-4.5A3.375 3.375 0 0012.75 10.875h-.75a3.375 3.375 0 00-3.375 3.375v4.5m9-9L12 3l-4.125 6.75" />',
                    color: 'text-warning',
                    description: '1000+ contributions'
                });
            } else if (totalActivity >= 500) {
                achievements.push({
                    name: 'Super Contributor',
                    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M11.48 3.499a.562.562 0 011.04 0l2.125 5.111a.563.563 0 00.475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 00-.182.557l1.285 5.385a.562.562 0 01-.84.61l-4.725-2.885a.563.563 0 00-.586 0L6.982 20.54a.562.562 0 01-.84-.61l1.285-5.386a.562.562 0 00-.182-.557l-4.204-3.602a.563.563 0 01.321-.988l5.518-.442a.563.563 0 00.475-.345L11.48 3.5z" />',
                    color: 'text-info',
                    description: '500+ contributions'
                });
            } else if (totalActivity >= 100) {
                achievements.push({
                    name: 'Active Member',
                    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09zM18.259 8.715L18 9.75l-.259-1.035a3.375 3.375 0 00-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 002.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 002.455 2.456L21.75 6l-1.036.259a3.375 3.375 0 00-2.455 2.456zM16.894 20.567L16.5 21.75l-.394-1.183a2.25 2.25 0 00-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 001.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 001.423 1.423l1.183.394-1.183.394a2.25 2.25 0 00-1.423 1.423z" />',
                    color: 'text-success',
                    description: '100+ contributions'
                });
            }

            // Discussion starter
            if (discussionCount >= 50) {
                achievements.push({
                    name: 'Discussion Starter',
                    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20.25 8.511c.884.284 1.5 1.128 1.5 2.097v4.286c0 1.136-.847 2.1-1.98 2.193-.34.027-.68.052-1.02.072v3.091l-3-3c-1.354 0-2.694-.055-4.02-.163a2.115 2.115 0 01-.825-.242m9.345-8.334a2.126 2.126 0 00-.476-.095 48.64 48.64 0 00-8.048 0c-1.131.094-1.976 1.057-1.976 2.192v4.286c0 .837.46 1.58 1.155 1.951m9.345-8.334V6.637c0-1.621-1.152-3.026-2.76-3.235A48.455 48.455 0 0011.25 3c-2.115 0-4.198.137-6.24.402-1.608.209-2.76 1.614-2.76 3.235v6.226c0 1.621 1.152 3.026 2.76 3.235.577.075 1.157.14 1.74.194V21l4.155-4.155" />',
                    color: 'text-primary',
                    description: '50+ discussions'
                });
            }

            // Conversationalist
            if (postCount >= 100 && postCount > discussionCount * 3) {
                achievements.push({
                    name: 'Conversationalist',
                    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M7.5 8.25h9m-9 3H12m-9.75 1.51c0 1.6 1.123 2.994 2.707 3.227 1.129.166 2.27.293 3.423.379.35.026.67.21.865.501L12 21l2.755-4.133a1.14 1.14 0 01.865-.501 48.172 48.172 0 003.423-.379c1.584-.233 2.707-1.626 2.707-3.228V6.741c0-1.602-1.123-2.995-2.707-3.228A48.394 48.394 0 0012 3c-2.392 0-4.744.175-7.043.513C3.373 3.746 2.25 5.14 2.25 6.741v6.018z" />',
                    color: 'text-accent',
                    description: 'Highly engaged in discussions'
                });
            }

            // Veteran / Regular
            if (daysSinceJoined >= 365) {
                achievements.push({
                    name: 'Veteran',
                    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z" />',
                    color: 'text-secondary',
                    description: 'Member for over a year'
                });
            } else if (daysSinceJoined >= 180) {
                achievements.push({
                    name: 'Regular',
                    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 012.25-2.25h13.5A2.25 2.25 0 0121 7.5v11.25m-18 0A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75m-18 0v-7.5A2.25 2.25 0 015.25 9h13.5A2.25 2.25 0 0121 11.25v7.5" />',
                    color: 'text-neutral-content',
                    description: 'Member for 6+ months'
                });
            }

            if (achievements.length === 0) return;

            grid.innerHTML = achievements.map(a => `
                <div class="achievement-item" title="${escapeHtml(a.description)}">
                    <div class="achievement-icon ${a.color}">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            ${a.icon}
                        </svg>
                    </div>
                    <span class="achievement-name">${escapeHtml(a.name)}</span>
                </div>
            `).join('');

            section.classList.remove('hidden');
        }

        // Initialize all sections
        loadUserStats();
        loadProfileActions();
        loadAchievements();
        loadActivityChart(30);
        loadRecentDiscussions(5);
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

    // Export only initializeProfile
    window.initializeProfile = initializeProfile;
})();
