"use strict";
/**
 * Authentication Navbar Management
 * Handles auth status loading, navbar rendering, and notifications
 */
// ============================================================================
// Implementation
// ============================================================================
(function () {
    'use strict';
    /**
     * Initialize auth navbar on page load
     */
    async function initAuthNavbar() {
        try {
            const response = await fetch('/bff/auth/status', { credentials: 'include' });
            const data = await response.json();
            updateDebugAuthInfo(data);
            if (data.isAuthenticated && data.publicId) {
                window.currentUserId = data.publicId; // Keep for backwards compat
                renderAuthenticatedNav(data);
                loadNotificationCount();
                loadNotifications();
            }
            else {
                renderUnauthenticatedNav();
            }
            // Dispatch event for other modules
            document.dispatchEvent(new CustomEvent('snakk:nav:loaded', {
                detail: { authenticated: data.isAuthenticated, user: data }
            }));
        }
        catch (err) {
            const errorMessage = err instanceof Error ? err.message : 'Unknown error';
            console.warn('[Auth Navbar] Failed to fetch auth status:', err);
            renderUnauthenticatedNav();
            updateDebugAuthInfo({ isAuthenticated: false, error: errorMessage });
        }
        finally {
            // Update theme toggle button icon
            window.snakkTheme?.updateToggleButton();
        }
    }
    /**
     * Render authenticated navbar
     */
    function renderAuthenticatedNav(user) {
        const authNav = document.getElementById('auth-nav');
        if (!authNav || !user.publicId || !user.displayName)
            return;
        const verifiedBadge = user.emailVerified ? '' :
            '<span class="badge badge-warning badge-xs ml-2">Unverified</span>';
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
                        <button data-action="mark-all-notifications-read" class="text-xs text-primary hover:underline">Mark all read</button>
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
                        <img src="${user.avatarUrl}"
                             alt="${escapeHtml(user.displayName)}"
                             loading="lazy" />
                    </div>
                </label>
                <ul tabindex="0" class="mt-3 z-[1] p-2 shadow-lg menu menu-sm dropdown-content bg-white border border-subtle rounded-lg w-52">
                    <li>
                        <a href="/u/${user.publicId}" class="font-semibold">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                            </svg>
                            ${escapeHtml(user.displayName)}
                            ${verifiedBadge}
                        </a>
                    </li>
                    <li>
                        <a href="/settings">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                            </svg>
                            Settings
                        </a>
                    </li>
                    <li>
                        <a href="#" id="theme-toggle" data-action="toggle-theme">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
                            </svg>
                            Toggle Theme
                        </a>
                    </li>
                    ${window.UserRoleType?.hasModeratorPrivileges(user.role) ? `
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
                    ` : ''}
                    ${window.UserRoleType?.isGlobalAdmin(user.role) ? `
                    <li>
                        <a href="/admin">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                            </svg>
                            Admin Panel
                        </a>
                    </li>
                    <li><hr class="my-1 border-subtle"/></li>
                    ` : ''}
                    <li>
                        <a href="#" data-action="logout">
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
        window.snakkTheme?.updateToggleButton();
    }
    /**
     * Render unauthenticated navbar
     */
    function renderUnauthenticatedNav() {
        const authNav = document.getElementById('auth-nav');
        if (!authNav)
            return;
        authNav.innerHTML = `
            <a href="/auth/login" class="btn btn-ghost btn-sm">Login</a>
            <a href="/auth/register" class="btn btn-primary btn-sm">Sign Up</a>
        `;
    }
    /**
     * Update debug auth info panel
     */
    function updateDebugAuthInfo(data) {
        const debugAuthInfo = document.getElementById('debug-auth-info');
        if (!debugAuthInfo)
            return;
        if (data.isAuthenticated && data.displayName && data.publicId) {
            const verifiedBadge = data.emailVerified
                ? '<span class="text-green-400">verified</span>'
                : '<span class="text-orange-400">unverified</span>';
            const roleBadge = data.role
                ? `<span class="text-gray-500">|</span>
                   <span class="text-gray-400">Role:</span>
                   <span class="text-purple-400 font-semibold uppercase">${escapeHtml(data.role)}</span>`
                : '';
            debugAuthInfo.innerHTML = `
                <span class="text-gray-400">Auth:</span>
                <span class="text-green-300">logged in</span>
                <span class="text-gray-500">|</span>
                <span class="text-gray-400">User:</span>
                <span class="text-cyan-300">${escapeHtml(data.displayName)}</span>
                <span class="text-gray-600">(${data.publicId})</span>
                <span class="text-gray-500">|</span>
                <span class="text-gray-400">Email:</span>
                ${verifiedBadge}
                ${roleBadge}
            `;
        }
        else {
            debugAuthInfo.innerHTML = `
                <span class="text-gray-400">Auth:</span>
                <span class="text-red-400">not logged in</span>
                ${data.error ? `<span class="text-gray-600">(${escapeHtml(data.error)})</span>` : ''}
            `;
        }
    }
    /**
     * Handle logout
     */
    async function handleLogout() {
        try {
            await fetch('/bff/auth/logout', {
                method: 'POST',
                credentials: 'include'
            });
        }
        catch (err) {
            console.warn('[Auth Navbar] Logout error:', err);
        }
        finally {
            // Force hard reload to clear any cached responses and ensure clean state
            window.location.replace('/');
        }
    }
    // ===== Notification Functions =====
    /**
     * Load notification count
     */
    async function loadNotificationCount() {
        try {
            const response = await fetch('/bff/notifications/unread-count', { credentials: 'include' });
            const data = await response.json();
            updateNotificationBadge(data.count);
        }
        catch (err) {
            console.warn('[Auth Navbar] Failed to load notification count:', err);
        }
    }
    /**
     * Update notification badge
     */
    function updateNotificationBadge(count) {
        const badge = document.getElementById('notification-badge');
        if (!badge)
            return;
        if (count > 0) {
            badge.textContent = count > 99 ? '99+' : count.toString();
            badge.classList.remove('hidden');
        }
        else {
            badge.classList.add('hidden');
        }
    }
    /**
     * Load notifications list
     */
    async function loadNotifications() {
        const list = document.getElementById('notification-list');
        if (!list)
            return;
        try {
            const response = await fetch('/bff/notifications?offset=0&pageSize=10', { credentials: 'include' });
            const data = await response.json();
            if (!data.items || data.items.length === 0) {
                list.innerHTML = '<p class="text-sm text-muted text-center py-4">No notifications yet</p>';
                return;
            }
            list.innerHTML = data.items.map(n => `
                <div class="notification-item ${n.isRead ? '' : 'unread'}" data-id="${n.publicId}">
                    <div class="flex items-start gap-2 p-2 rounded hover:bg-subtle cursor-pointer"
                         data-action="click-notification"
                         data-notification-id="${n.publicId}"
                         data-discussion-id="${n.sourceDiscussionId || ''}">
                        <div class="notification-icon ${getNotificationIconClass(n.type)}">
                            ${getNotificationIcon(n.type)}
                        </div>
                        <div class="flex-1 min-w-0">
                            <p class="text-sm font-medium truncate">${escapeHtml(n.title)}</p>
                            ${n.body ? `<p class="text-xs text-muted line-clamp-2">${escapeHtml(n.body)}</p>` : ''}
                            <p class="text-xs text-muted mt-1">${formatTimeAgo(n.createdAt)}</p>
                        </div>
                    </div>
                </div>
            `).join('');
        }
        catch (err) {
            console.warn('[Auth Navbar] Failed to load notifications:', err);
            list.innerHTML = '<p class="text-sm text-error text-center py-4">Failed to load</p>';
        }
    }
    /**
     * Handle notification click
     */
    async function handleNotificationClick(element) {
        const notificationId = element.dataset.notificationId;
        const discussionId = element.dataset.discussionId;
        if (!notificationId)
            return;
        try {
            // Mark as read
            await fetch(`/bff/notifications/${notificationId}/read`, {
                method: 'POST',
                credentials: 'include'
            });
            loadNotificationCount();
            const item = document.querySelector(`[data-id="${notificationId}"]`);
            if (item)
                item.classList.remove('unread');
            // Navigate to discussion if available
            if (discussionId) {
                // For now, just reload - in production would navigate to the discussion
                // window.location.href = `/discussions/${discussionId}`;
            }
        }
        catch (err) {
            console.warn('[Auth Navbar] Failed to mark notification as read:', err);
        }
    }
    /**
     * Mark all notifications as read
     */
    async function markAllNotificationsAsRead() {
        try {
            await fetch('/bff/notifications/read-all', {
                method: 'POST',
                credentials: 'include'
            });
            updateNotificationBadge(0);
            document.querySelectorAll('.notification-item.unread').forEach(el => {
                el.classList.remove('unread');
            });
        }
        catch (err) {
            console.warn('[Auth Navbar] Failed to mark all notifications as read:', err);
        }
    }
    // ===== Helper Functions =====
    function getNotificationIconClass(type) {
        const classes = {
            'Reply': 'text-primary',
            'Mention': 'text-accent'
        };
        return classes[type] || 'text-muted';
    }
    function getNotificationIcon(type) {
        const icons = {
            'Reply': '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" /></svg>',
            'Mention': '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 12a4 4 0 10-8 0 4 4 0 008 0zm0 0v1.5a2.5 2.5 0 005 0V12a9 9 0 10-9 9m4.5-1.206a8.959 8.959 0 01-4.5 1.207" /></svg>',
            'NewPostInFollowedDiscussion': '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 8h10M7 12h4m1 8l-4-4H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-3l-4 4z" /></svg>'
        };
        return icons[type] || '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" /></svg>';
    }
    function formatTimeAgo(dateString) {
        const date = new Date(dateString);
        const now = new Date();
        const diffMs = now.getTime() - date.getTime();
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);
        if (diffMins < 1)
            return 'just now';
        if (diffMins < 60)
            return `${diffMins}m ago`;
        if (diffHours < 24)
            return `${diffHours}h ago`;
        if (diffDays < 7)
            return `${diffDays}d ago`;
        return date.toLocaleDateString();
    }
    function escapeHtml(text) {
        if (!text)
            return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
    // ===== Event Delegation =====
    document.addEventListener('click', async (event) => {
        const target = event.target;
        const action = target.closest('[data-action]');
        if (!action)
            return;
        event.preventDefault();
        const actionName = action.dataset.action;
        switch (actionName) {
            case 'logout':
                await handleLogout();
                break;
            case 'toggle-theme':
                window.snakkTheme?.toggleTheme();
                break;
            case 'mark-all-notifications-read':
                await markAllNotificationsAsRead();
                break;
            case 'click-notification':
                await handleNotificationClick(action);
                break;
        }
    });
    // ===== Listen for Realtime Events =====
    document.addEventListener('snakk:realtime:notification-count', (event) => {
        const customEvent = event;
        updateNotificationBadge(customEvent.detail.unreadCount);
    });
    document.addEventListener('snakk:realtime:notification', () => {
        loadNotificationCount();
        loadNotifications();
    });
    // ===== Initialize =====
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAuthNavbar);
    }
    else {
        initAuthNavbar();
    }
    // Export minimal API (for backwards compatibility and custom events)
    window.SnakkAuthNav = {
        refresh: initAuthNavbar,
        updateNotificationBadge
    };
})();
//# sourceMappingURL=auth-navbar.js.map