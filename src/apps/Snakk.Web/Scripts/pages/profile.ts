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

            const shouldGroupByWeek = days > 30;
            let chartData = data;

            if (shouldGroupByWeek) {
                const grouped: ActivityDataPoint[] = [];
                for (let i = 0; i < data.length; i += 7) {
                    const week = data.slice(i, i + 7);
                    if (week.length === 0 || !week[0]) continue;
                    grouped.push({
                        date: week[0].date,
                        discussions: week.reduce((s: number, d: ActivityDataPoint) => s + d.discussions, 0),
                        posts:       week.reduce((s: number, d: ActivityDataPoint) => s + d.posts, 0),
                        total:       week.reduce((s: number, d: ActivityDataPoint) => s + d.total, 0),
                        isWeek: true,
                    });
                }
                chartData = grouped;
            }

            const maxValue         = Math.max(...chartData.map((d: ActivityDataPoint) => d.total), 1);
            const totalDiscussions = data.reduce((s: number, d: ActivityDataPoint) => s + d.discussions, 0);
            const totalPosts       = data.reduce((s: number, d: ActivityDataPoint) => s + d.posts, 0);
            const totalActivity    = totalDiscussions + totalPosts;

            // Read container width before any DOM mutations — avoids a forced
            // layout flush after innerHTML change that can expose a blank canvas frame.
            const containerWidth = container.clientWidth || container.offsetWidth;

            // Sample bar colors from CSS so the canvas respects context overrides (e.g. .sn-profile-card dark bg)
            function sampleBg(cls: string): string {
                const el = document.createElement('div');
                el.className = cls;
                el.style.cssText = 'position:absolute;width:1px;height:1px;opacity:0;pointer-events:none';
                container.appendChild(el);
                const color = getComputedStyle(el).backgroundColor;
                container.removeChild(el);
                return color;
            }
            const colorPrimary   = sampleBg('sn-activity-chart-bar-segment-primary');
            const colorSecondary = sampleBg('sn-activity-chart-bar-segment-secondary');
            const colorZero      = sampleBg('sn-activity-chart-bar-zero');

            // Build and draw the canvas off-DOM so the skeleton is replaced atomically
            // with already-painted content — prevents the compositor flash caused by
            // the skeleton's will-change:opacity layer tearing down before new pixels arrive.
            const canvas = document.createElement('canvas');
            canvas.className = 'sn-activity-canvas';
            canvas.style.height = `${maxHeight}px`;
            canvas.setAttribute('aria-label', 'Activity history chart');
            canvas.setAttribute('role', 'img');

            const dpr = window.devicePixelRatio || 1;
            const w   = containerWidth;
            const h   = maxHeight;
            canvas.width  = Math.round(w * dpr);
            canvas.height = Math.round(h * dpr);

            const ctx = canvas.getContext('2d');
            if (!ctx) return;
            ctx.scale(dpr, dpr);

            const n    = chartData.length;
            const GAP  = 2;
            const barW = Math.max(1, (w - GAP * (n - 1)) / n);
            const R    = 2;

            const barRects: Array<{ x: number; y: number; w: number; h: number; d: ActivityDataPoint }> = [];

            chartData.forEach((d: ActivityDataPoint, i: number) => {
                const x = i * (barW + GAP);

                if (d.total === 0) {
                    ctx.fillStyle = colorZero;
                    ctx.beginPath();
                    ctx.roundRect(x, h - 4, barW, 4, [R, R, 0, 0]);
                    ctx.fill();
                    barRects.push({ x, y: h - 4, w: barW, h: 4, d });
                    return;
                }

                const totalH = Math.max(4, (d.total / maxValue) * h);
                const barY   = h - totalH;
                const discsH = d.discussions > 0 ? (d.discussions / d.total) * totalH : 0;
                const postsH = totalH - discsH;

                if (discsH > 0) {
                    ctx.fillStyle = colorPrimary;
                    ctx.beginPath();
                    ctx.roundRect(x, barY, barW, discsH, [R, R, 0, 0]);
                    ctx.fill();
                }
                if (postsH > 0) {
                    ctx.fillStyle = colorSecondary;
                    ctx.beginPath();
                    ctx.roundRect(x, barY + discsH, barW, postsH, discsH > 0 ? [0, 0, 0, 0] : [R, R, 0, 0]);
                    ctx.fill();
                }

                barRects.push({ x, y: barY, w: barW, h: totalH, d });
            });

            const legend = document.createElement('div');
            legend.className = 'sn-activity-chart-legend';
            legend.innerHTML = `
                <div class="sn-activity-chart-legend-item">
                    <div class="sn-activity-chart-legend-color sn-activity-chart-legend-color-primary"></div>
                    <span>${totalDiscussions} discussion${totalDiscussions !== 1 ? 's' : ''}</span>
                </div>
                <div class="sn-activity-chart-legend-item">
                    <div class="sn-activity-chart-legend-color sn-activity-chart-legend-color-secondary"></div>
                    <span>${totalPosts} post${totalPosts !== 1 ? 's' : ''}</span>
                </div>
                <span class="sn-activity-chart-legend-total">(${totalActivity} total)</span>
            `;

            const tooltip = document.createElement('div');
            tooltip.className = 'sn-activity-chart-tooltip';

            // Atomically replace skeleton with the fully-drawn canvas
            container.style.position = 'relative';
            container.replaceChildren(canvas, legend, tooltip);

            const tz       = (window as any).snakkTimezone || 'UTC';
            const dateOpts: Intl.DateTimeFormatOptions = { timeZone: tz, month: 'short', day: 'numeric' };
            const fmtDate  = (s: string) => {
                try { return new Date(s).toLocaleDateString('en-US', dateOpts); }
                catch { return new Date(s).toLocaleDateString('en-US', { month: 'short', day: 'numeric' }); }
            };

            canvas.addEventListener('mousemove', (e: MouseEvent) => {
                const rect = canvas.getBoundingClientRect();
                const mx   = e.clientX - rect.left;
                const hit  = barRects.find(r => mx >= r.x && mx <= r.x + r.w);
                if (!hit) { tooltip.style.display = 'none'; return; }

                const label = shouldGroupByWeek ? `Week of ${fmtDate(hit.d.date)}` : fmtDate(hit.d.date);
                tooltip.innerHTML = `<div>${hit.d.total} contribution${hit.d.total !== 1 ? 's' : ''}</div><div>${hit.d.discussions} discussion${hit.d.discussions !== 1 ? 's' : ''}</div><div>${hit.d.posts} post${hit.d.posts !== 1 ? 's' : ''}</div><div>${label}</div>`;
                tooltip.style.display   = 'block';
                tooltip.style.left      = `${hit.x + hit.w / 2}px`;
                tooltip.style.top       = `${hit.y - 4}px`;
                tooltip.style.transform = 'translate(-50%, -100%)';
            });

            canvas.addEventListener('mouseleave', () => { tooltip.style.display = 'none'; });
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
