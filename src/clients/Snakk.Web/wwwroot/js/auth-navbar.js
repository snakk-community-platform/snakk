// Authentication Navbar - Load auth status and render navbar UI

(function() {
    'use strict';

    // Initialize auth navbar on page load
    function initAuthNavbar() {
        // Don't proactively clear tokens - let the server decide if they're valid
        // Rapid refreshes can cause race conditions if we clear tokens preemptively

        fetch(`${window.apiBaseUrl}/auth/status`, { credentials: 'include' })
            .then(res => {
                // Only clear tokens on explicit auth failures (401/403), not network errors
                if (res.status === 401 || res.status === 403) {
                    window.snakkAuth.clearToken();
                }
                return res.json();
            })
            .then(data => {
                const authNav = document.getElementById('auth-nav');
                // Update debug pane
                updateDebugAuthInfo(data);
                if (data.isAuthenticated) {
                    window.currentUserId = data.publicId;
                    authNav.innerHTML = `
                        <!-- Notification Bell -->
                        <div class="dropdown dropdown-end mr-2">
                            <label tabindex="0" class="btn btn-ghost btn-sm btn-circle relative">
                                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
                                </svg>
                                <span id="notification-badge" class="notification-badge hidden">0</span>
                            </label>
                            <div tabindex="0" class="dropdown-content z-[1] mt-3 w-80 max-h-96 overflow-y-auto shadow-lg bg-white border border-subtle rounded-lg">
                                <div class="flex items-center justify-between p-3 border-b border-subtle">
                                    <span class="font-semibold">Notifications</span>
                                    <button onclick="markAllNotificationsAsRead()" class="text-xs text-primary hover:underline">Mark all read</button>
                                </div>
                                <div id="notification-list" class="p-2">
                                    <p class="text-sm text-muted text-center py-4">Loading...</p>
                                </div>
                            </div>
                        </div>
                        <!-- User Menu -->
                        <div class="dropdown dropdown-end">
                            <label tabindex="0" class="btn btn-ghost btn-sm btn-circle p-0">
                                <div class="avatar avatar-sm">
                                    <img src="${window.apiBaseUrl}/avatars/${data.publicId}"
                                         alt="${data.displayName}"
                                         loading="lazy" />
                                </div>
                            </label>
                            <ul tabindex="0" class="mt-3 z-[1] p-2 shadow-lg menu menu-sm dropdown-content bg-white border border-subtle rounded-lg w-52">
                                <li>
                                    <a href="/u/${data.publicId}" class="font-semibold">
                                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                                        </svg>
                                        ${data.displayName}
                                        ${data.emailVerified ? '' : '<span class="badge badge-warning-subtle badge-xs ml-2">Unverified</span>'}
                                    </a>
                                </li>
                                <li>
                                    <a href="/auth/profile">
                                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                                        </svg>
                                        Profile Settings
                                    </a>
                                </li>
                                <li>
                                    <a id="theme-toggle" href="#" onclick="window.snakkTheme.toggleTheme(); return false;">
                                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
                                        </svg>
                                        Toggle Theme
                                    </a>
                                </li>
                                <li><hr class="my-1 border-subtle"/></li>
                                <li>
                                    <a href="/moderation">
                                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                                        </svg>
                                        Moderation
                                    </a>
                                </li>
                                <li><hr class="my-1 border-subtle"/></li>
                                <li>
                                    <a href="#" onclick="logout(); return false;">
                                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                                        </svg>
                                        Logout
                                    </a>
                                </li>
                            </ul>
                        </div>
                    `;
                    // Update theme toggle button with current state
                    if (window.snakkTheme) {
                        window.snakkTheme.updateToggleButton();
                    }
                    // Load notification count and list
                    loadNotificationCount();
                    loadNotifications();
                } else {
                    // Server says not authenticated - clear any stale/invalid token
                    window.snakkAuth.clearToken();
                    authNav.innerHTML = `
                        <a href="/auth/login" class="btn btn-ghost btn-sm">Login</a>
                        <a href="/auth/register" class="btn btn-primary btn-sm">Sign Up</a>
                    `;
                }
            })
            .catch(err => {
                // Network error - do NOT clear tokens (might be temporary)
                // Tokens are only cleared on explicit 401/403 responses above
                console.warn('[Auth Navbar] Failed to fetch auth status:', err);
                const authNav = document.getElementById('auth-nav');
                authNav.innerHTML = `
                    <a href="/auth/login" class="btn btn-ghost btn-sm">Login</a>
                    <a href="/auth/register" class="btn btn-primary btn-sm">Sign Up</a>
                `;
                // Update debug pane
                updateDebugAuthInfo({ isAuthenticated: false, error: err.message });
            })
            .finally(() => {
                // Update theme toggle button icon
                window.snakkTheme.updateToggleButton();
            });
    }

    function updateDebugAuthInfo(data) {
        const debugAuthInfo = document.getElementById('debug-auth-info');
        if (!debugAuthInfo) return;

        if (data.isAuthenticated) {
            const verifiedBadge = data.emailVerified
                ? '<span class="text-green-400">verified</span>'
                : '<span class="text-orange-400">unverified</span>';
            debugAuthInfo.innerHTML = `
                <span class="text-gray-400">Auth:</span>
                <span class="text-green-300">logged in</span>
                <span class="text-gray-500">|</span>
                <span class="text-gray-400">User:</span>
                <span class="text-cyan-300">${data.displayName}</span>
                <span class="text-gray-600">(${data.publicId})</span>
                <span class="text-gray-500">|</span>
                <span class="text-gray-400">Email:</span>
                ${verifiedBadge}
            `;
        } else {
            debugAuthInfo.innerHTML = `
                <span class="text-gray-400">Auth:</span>
                <span class="text-red-400">not logged in</span>
                ${data.error ? `<span class="text-gray-600">(${data.error})</span>` : ''}
            `;
        }
    }

    function logout() {
        fetch(`${window.apiBaseUrl}/auth/logout`, { method: 'POST', credentials: 'include' })
            .then(() => {
                window.snakkAuth.clearToken();
                window.location.href = '/';
            });
    }

    // ===== Notification Functions =====
    function loadNotificationCount() {
        fetch(`/bff/notifications/unread-count`, { credentials: 'include' })
            .then(res => res.json())
            .then(data => {
                updateNotificationBadge(data.count);
            })
            .catch(() => {});
    }

    function updateNotificationBadge(count) {
        const badge = document.getElementById('notification-badge');
        if (badge) {
            if (count > 0) {
                badge.textContent = count > 99 ? '99+' : count;
                badge.classList.remove('hidden');
            } else {
                badge.classList.add('hidden');
            }
        }
    }

    function loadNotifications() {
        const list = document.getElementById('notification-list');
        if (!list) return;

        fetch(`/bff/notifications?offset=0&pageSize=10`, { credentials: 'include' })
            .then(res => res.json())
            .then(data => {
                if (!data.items || data.items.length === 0) {
                    list.innerHTML = '<p class="text-sm text-muted text-center py-4">No notifications yet</p>';
                    return;
                }

                list.innerHTML = data.items.map(n => `
                    <div class="notification-item ${n.isRead ? '' : 'unread'}" data-id="${n.publicId}">
                        <div class="flex items-start gap-2 p-2 rounded hover:bg-subtle cursor-pointer" onclick="handleNotificationClick('${n.publicId}', '${n.sourceDiscussionId || ''}')">
                            <div class="notification-icon ${getNotificationIconClass(n.type)}">
                                ${getNotificationIcon(n.type)}
                            </div>
                            <div class="flex-1 min-w-0">
                                <p class="text-sm font-medium truncate">${n.title}</p>
                                ${n.body ? `<p class="text-xs text-muted line-clamp-2">${n.body}</p>` : ''}
                                <p class="text-xs text-muted mt-1">${formatTimeAgo(n.createdAt)}</p>
                            </div>
                        </div>
                    </div>
                `).join('');
            })
            .catch(() => {
                list.innerHTML = '<p class="text-sm text-error text-center py-4">Failed to load</p>';
            });
    }

    function getNotificationIconClass(type) {
        switch(type) {
            case 'Reply': return 'text-primary';
            case 'Mention': return 'text-accent';
            default: return 'text-muted';
        }
    }

    function getNotificationIcon(type) {
        switch(type) {
            case 'Reply':
                return '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" /></svg>';
            case 'Mention':
                return '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 12a4 4 0 10-8 0 4 4 0 008 0zm0 0v1.5a2.5 2.5 0 005 0V12a9 9 0 10-9 9m4.5-1.206a8.959 8.959 0 01-4.5 1.207" /></svg>';
            case 'NewPostInFollowedDiscussion':
                return '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 8h10M7 12h4m1 8l-4-4H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-3l-4 4z" /></svg>';
            default:
                return '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" /></svg>';
        }
    }

    function formatTimeAgo(dateString) {
        const date = new Date(dateString);
        const now = new Date();
        const diffMs = now - date;
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);

        if (diffMins < 1) return 'just now';
        if (diffMins < 60) return `${diffMins}m ago`;
        if (diffHours < 24) return `${diffHours}h ago`;
        if (diffDays < 7) return `${diffDays}d ago`;
        return date.toLocaleDateString();
    }

    function handleNotificationClick(notificationId, discussionId) {
        // Mark as read
        fetch(`/bff/notifications/${notificationId}/read`, {
            method: 'POST',
            credentials: 'include'
        }).then(() => {
            loadNotificationCount();
            const item = document.querySelector(`[data-id="${notificationId}"]`);
            if (item) item.classList.remove('unread');
        });

        // Navigate to discussion if available
        if (discussionId) {
            // For now, just reload - in production would navigate to the discussion
            // window.location.href = `/discussions/${discussionId}`;
        }
    }

    function markAllNotificationsAsRead() {
        fetch(`/bff/notifications/read-all`, {
            method: 'POST',
            credentials: 'include'
        }).then(() => {
            updateNotificationBadge(0);
            document.querySelectorAll('.notification-item.unread').forEach(el => {
                el.classList.remove('unread');
            });
        });
    }

    // Expose functions globally for onclick handlers
    window.logout = logout;
    window.handleNotificationClick = handleNotificationClick;
    window.markAllNotificationsAsRead = markAllNotificationsAsRead;

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAuthNavbar);
    } else {
        initAuthNavbar();
    }
})();
