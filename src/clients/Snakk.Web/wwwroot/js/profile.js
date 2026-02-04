// User Profile Page JavaScript

// Initialize profile page with data from server
function initializeProfile(userId, currentTab, stats) {
    // Load user stats
    async function loadUserStats() {
        try {
            const response = await fetch(`${window.apiBaseUrl}/api/users/${userId}/stats`);
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
    async function loadRecentDiscussions(limit) {
        const container = document.getElementById('recent-discussions');
        if (!container) return;

        try {
            const response = await fetch(`${window.apiBaseUrl}/api/search/discussions?authorPublicId=${userId}&pageSize=${limit}`);
            const data = await response.json();

            if (!data.items || data.items.length === 0) {
                container.innerHTML = `
                    <div class="text-center py-8 text-muted">
                        <p>No discussions yet</p>
                    </div>
                `;
                return;
            }

            container.innerHTML = data.items.map(discussion => `
                <a href="${discussion.url}" class="block hover:bg-base-200 p-3 rounded transition-colors">
                    <h4 class="font-medium mb-1">${escapeHtml(discussion.title)}</h4>
                    <div class="flex items-center gap-4 text-sm text-muted">
                        <span>${discussion.replyCount} ${discussion.replyCount === 1 ? 'reply' : 'replies'}</span>
                        <span>${formatRelativeTime(discussion.createdAt)}</span>
                    </div>
                </a>
            `).join('');
        } catch (error) {
            console.error('Error loading discussions:', error);
            container.innerHTML = '<div class="text-center py-8 text-error">Failed to load discussions</div>';
        }
    }

    // Recent posts
    async function loadRecentPosts(limit) {
        const container = document.getElementById('recent-posts');
        if (!container) return;

        try {
            const response = await fetch(`${window.apiBaseUrl}/api/search/posts?authorPublicId=${userId}&pageSize=${limit}`);
            const data = await response.json();

            if (!data.items || data.items.length === 0) {
                container.innerHTML = `
                    <div class="text-center py-8 text-muted">
                        <p>No posts yet</p>
                    </div>
                `;
                return;
            }

            container.innerHTML = data.items.map(post => `
                <a href="${post.discussionUrl}" class="block hover:bg-base-200 p-3 rounded transition-colors">
                    <div class="prose prose-sm max-w-none mb-2">
                        ${post.contentPreview}
                    </div>
                    <div class="flex items-center gap-4 text-sm text-muted">
                        <span>in ${escapeHtml(post.discussionTitle)}</span>
                        <span>${formatRelativeTime(post.createdAt)}</span>
                    </div>
                </a>
            `).join('');
        } catch (error) {
            console.error('Error loading posts:', error);
            container.innerHTML = '<div class="text-center py-8 text-error">Failed to load posts</div>';
        }
    }

    // All discussions (paginated)
    let currentDiscussionPage = 0;
    async function loadAllDiscussions() {
        const container = document.getElementById('all-discussions');
        if (!container) return;

        try {
            const response = await fetch(`${window.apiBaseUrl}/api/search/discussions?authorPublicId=${userId}&pageSize=20`);
            const data = await response.json();

            if (!data.items || data.items.length === 0) {
                container.innerHTML = `
                    <div class="text-center py-12">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-16 w-16 mx-auto text-muted mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z" />
                        </svg>
                        <h3 class="font-semibold mb-2">No discussions yet</h3>
                        <p class="text-sm text-muted">This user hasn't started any discussions</p>
                    </div>
                `;
                return;
            }

            container.innerHTML = data.items.map(discussion => `
                <div class="clean-card hover:shadow-md transition-shadow">
                    <a href="${discussion.url}" class="block p-4">
                        <h3 class="font-semibold mb-2">${escapeHtml(discussion.title)}</h3>
                        <div class="flex items-center gap-4 text-sm text-muted">
                            <span>${discussion.replyCount} ${discussion.replyCount === 1 ? 'reply' : 'replies'}</span>
                            <span>${formatRelativeTime(discussion.createdAt)}</span>
                            <span class="ml-auto">${escapeHtml(discussion.spaceName)}</span>
                        </div>
                    </a>
                </div>
            `).join('');
        } catch (error) {
            console.error('Error loading all discussions:', error);
            container.innerHTML = '<div class="text-center py-8 text-error">Failed to load discussions</div>';
        }
    }

    // All posts (paginated)
    let currentPostPage = 0;
    async function loadAllPosts() {
        const container = document.getElementById('all-posts');
        if (!container) return;

        try {
            const response = await fetch(`${window.apiBaseUrl}/api/search/posts?authorPublicId=${userId}&pageSize=20`);
            const data = await response.json();

            if (!data.items || data.items.length === 0) {
                container.innerHTML = `
                    <div class="text-center py-12">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-16 w-16 mx-auto text-muted mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 8h10M7 12h4m1 8l-4-4H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-3l-4 4z" />
                        </svg>
                        <h3 class="font-semibold mb-2">No posts yet</h3>
                        <p class="text-sm text-muted">This user hasn't made any posts</p>
                    </div>
                `;
                return;
            }

            container.innerHTML = data.items.map(post => `
                <div class="clean-card hover:shadow-md transition-shadow">
                    <a href="${post.discussionUrl}" class="block p-4">
                        <div class="prose prose-sm max-w-none mb-3">
                            ${post.contentPreview}
                        </div>
                        <div class="flex items-center gap-4 text-sm text-muted">
                            <span>in ${escapeHtml(post.discussionTitle)}</span>
                            <span>${formatRelativeTime(post.createdAt)}</span>
                            <span class="ml-auto">${escapeHtml(post.spaceName)}</span>
                        </div>
                    </a>
                </div>
            `).join('');
        } catch (error) {
            console.error('Error loading all posts:', error);
            container.innerHTML = '<div class="text-center py-8 text-error">Failed to load posts</div>';
        }
    }

    // Activity Chart
    let currentChartDays = 30;

    async function loadActivityChart(days) {
        currentChartDays = days;

        // Update button states
        ['14', '30', '90'].forEach(d => {
            const btn = document.getElementById(`chart-${d}`);
            if (btn) {
                if (d === days.toString()) {
                    btn.classList.add('btn-active');
                } else {
                    btn.classList.remove('btn-active');
                }
            }
        });

        const container = document.getElementById('activity-chart');
        if (!container) return;

        try {
            const response = await fetch(`${window.apiBaseUrl}/api/users/${userId}/activity-history?days=${days}`);
            const result = await response.json();

            renderActivityChart(container, result.data, days);
        } catch (error) {
            console.error('Error loading activity chart:', error);
            container.innerHTML = '<div class="text-center py-8 text-error">Failed to load activity chart</div>';
        }
    }

    function renderActivityChart(container, data, days) {
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
        const maxValue = Math.max(...data.map(d => d.total), 1);
        const maxHeight = 150; // pixels

        // Group by week for better visualization if > 30 days
        const shouldGroupByWeek = days > 30;
        let chartData = data;

        if (shouldGroupByWeek) {
            const grouped = [];
            for (let i = 0; i < data.length; i += 7) {
                const week = data.slice(i, i + 7);
                const weekTotal = {
                    date: week[0].date,
                    discussions: week.reduce((sum, d) => sum + d.discussions, 0),
                    posts: week.reduce((sum, d) => sum + d.posts, 0),
                    total: week.reduce((sum, d) => sum + d.total, 0),
                    isWeek: true
                };
                grouped.push(weekTotal);
            }
            chartData = grouped;
        }

        const barsHtml = chartData.map((day, index) => {
            const heightPercent = maxValue > 0 ? (day.total / maxValue) * 100 : 0;
            const discussionsPercent = day.total > 0 ? (day.discussions / day.total) * 100 : 0;
            const postsPercent = day.total > 0 ? (day.posts / day.total) * 100 : 0;

            const dateLabel = shouldGroupByWeek
                ? `Week of ${new Date(day.date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}`
                : new Date(day.date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });

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

        const totalDiscussions = data.reduce((sum, d) => sum + d.discussions, 0);
        const totalPosts = data.reduce((sum, d) => sum + d.posts, 0);
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

    // Top Contributions
    async function loadTopContributions() {
        const container = document.getElementById('top-contributions');
        if (!container) return;

        try {
            const response = await fetch(`${window.apiBaseUrl}/api/search/discussions?authorPublicId=${userId}&pageSize=3`);
            const data = await response.json();

            if (!data.items || data.items.length === 0) {
                container.innerHTML = `
                    <div class="text-center py-6 text-muted">
                        <p>No discussions yet</p>
                    </div>
                `;
                return;
            }

            container.innerHTML = data.items.map((discussion, index) => `
                <div class="flex items-start gap-3">
                    <div class="flex-shrink-0 w-8 h-8 rounded-full bg-primary text-primary-content flex items-center justify-center font-semibold">
                        ${index + 1}
                    </div>
                    <div class="flex-1 min-w-0">
                        <a href="${discussion.url}" class="font-medium hover:underline block truncate">
                            ${escapeHtml(discussion.title)}
                        </a>
                        <div class="text-sm text-muted">
                            ${discussion.replyCount} ${discussion.replyCount === 1 ? 'reply' : 'replies'}
                        </div>
                    </div>
                </div>
            `).join('');
        } catch (error) {
            console.error('Error loading top contributions:', error);
            container.innerHTML = '<div class="text-center py-6 text-error">Failed to load</div>';
        }
    }

    // Profile Actions (Follow/Unfollow)
    async function loadProfileActions() {
        const container = document.getElementById('profile-actions');
        if (!container) return;

        try {
            // Check if user is authenticated and viewing someone else's profile
            const authResponse = await fetch(`${window.apiBaseUrl}/api/auth/status`, { credentials: 'include' });
            const authData = await authResponse.json();

            if (!authData.isAuthenticated) {
                container.innerHTML = ''; // No actions for anonymous users
                return;
            }

            if (authData.publicId === userId) {
                // Viewing own profile
                container.innerHTML = `
                    <a href="/auth/profile" class="btn btn-outline btn-sm">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                        </svg>
                        Edit Profile
                    </a>
                `;
                return;
            }

            // Check follow status
            const followResponse = await fetch(`${window.apiBaseUrl}/api/users/${userId}/follow-status?currentUserId=${authData.publicId}`, {
                credentials: 'include'
            });
            const followData = await followResponse.json();

            container.innerHTML = `
                <button onclick="toggleFollowUser()" class="btn ${followData.isFollowing ? 'btn-outline' : 'btn-primary'} btn-sm" id="follow-btn">
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

    // Toggle follow/unfollow - exposed globally for onclick handler
    window.toggleFollowUser = async function() {
        const btn = document.getElementById('follow-btn');
        const btnText = document.getElementById('follow-btn-text');
        if (!btn || !btnText) return;

        const wasFollowing = btnText.textContent === 'Following';
        btn.disabled = true;

        try {
            const response = await fetch(`${window.apiBaseUrl}/api/users/${userId}/follow`, {
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

    // Generate badges based on activity
    function loadUserBadges() {
        const container = document.getElementById('user-badges');
        if (!container) return;

        const badges = [];
        const totalActivity = stats.totalActivity;
        const daysSinceJoined = stats.daysSinceJoined;
        const discussionCount = stats.discussionCount;
        const postCount = stats.postCount;

        // Activity level badges
        if (totalActivity >= 1000) {
            badges.push({ text: '🏆 Power User', color: 'badge-warning', title: '1000+ contributions' });
        } else if (totalActivity >= 500) {
            badges.push({ text: '⭐ Super Contributor', color: 'badge-info', title: '500+ contributions' });
        } else if (totalActivity >= 100) {
            badges.push({ text: '✨ Active Member', color: 'badge-success', title: '100+ contributions' });
        }

        // Discussion starter badge
        if (discussionCount >= 50) {
            badges.push({ text: '💬 Discussion Starter', color: 'badge-primary', title: '50+ discussions' });
        }

        // Engagement badge (lots of posts relative to discussions)
        if (postCount >= 100 && postCount > discussionCount * 3) {
            badges.push({ text: '🗣️ Conversationalist', color: 'badge-accent', title: 'Highly engaged in discussions' });
        }

        // Veteran badge
        if (daysSinceJoined >= 365) {
            badges.push({ text: '🎖️ Veteran', color: 'badge-secondary', title: 'Member for over a year' });
        } else if (daysSinceJoined >= 180) {
            badges.push({ text: '📅 Regular', color: 'badge-neutral', title: 'Member for 6+ months' });
        }

        if (badges.length > 0) {
            container.innerHTML = badges.map(badge =>
                `<div class="badge ${badge.color} badge-sm" title="${badge.title}">${badge.text}</div>`
            ).join('');
        }
    }

    // Initialize page based on current tab
    loadUserStats();
    loadProfileActions();
    loadUserBadges();

    if (currentTab === 'overview') {
        loadActivityChart(30);
        loadTopContributions();
        loadRecentDiscussions(5);
        loadRecentPosts(5);
    } else if (currentTab === 'discussions') {
        loadAllDiscussions();
    } else if (currentTab === 'posts') {
        loadAllPosts();
    }

    // Expose loadActivityChart globally for button onclick handlers
    window.loadActivityChart = loadActivityChart;
}

// Utility functions
function escapeHtml(unsafe) {
    if (!unsafe) return '';
    return unsafe
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}

function formatRelativeTime(dateString) {
    const date = new Date(dateString);
    const now = new Date();
    const seconds = Math.floor((now - date) / 1000);

    if (seconds < 60) return 'just now';
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
    if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
    if (seconds < 604800) return `${Math.floor(seconds / 86400)}d ago`;

    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}
