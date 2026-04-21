/**
 * Auth Navbar — Notifications & Actions
 * Navbar HTML is server-rendered in _AuthNav.cshtml.
 * This module handles notification loading, realtime updates,
 * and event delegation for logout/theme/notification actions.
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface SnakkNotification {
    publicId: string;
    type: string;
    title: string;
    body?: string;
    isRead: boolean;
    createdAt: string;
    sourceDiscussionId?: string;
}

interface NotificationCountResponse {
    count: number;
}

interface NotificationsResponse {
    items?: SnakkNotification[];
}

// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    // ===== Notification Functions =====

    async function loadNotificationCount(): Promise<void> {
        try {
            const response = await fetch('/bff/notifications/unread-count', { credentials: 'include' });
            const data: NotificationCountResponse = await response.json();
            updateNotificationBadge(data.count);
        } catch (err) {
            console.warn('[Auth Navbar] Failed to load notification count:', err);
        }
    }

    function updateNotificationBadge(count: number): void {
        const badge = document.getElementById('notification-badge');
        if (!badge) return;

        if (count > 0) {
            badge.textContent = count > 99 ? '99+' : count.toString();
            badge.classList.remove('hidden');
        } else {
            badge.classList.add('hidden');
        }
    }

    async function loadNotifications(): Promise<void> {
        const list = document.getElementById('notification-list');
        if (!list) return;

        try {
            const response = await fetch('/bff/notifications?offset=0&pageSize=10', { credentials: 'include' });
            const data: NotificationsResponse = await response.json();

            if (!data.items || data.items.length === 0) {
                list.innerHTML = '<p class="text-sm text-muted text-center py-4">No notifications yet</p>';
                return;
            }

            list.innerHTML = data.items.map(n => `
                <div class="notification-item ${n.isRead ? '' : 'unread'}" data-id="${escapeHtml(n.publicId)}">
                    <div class="flex items-start gap-2 p-2 rounded hover:bg-subtle cursor-pointer"
                         data-action="click-notification"
                         data-notification-id="${escapeHtml(n.publicId)}"
                         data-discussion-id="${escapeHtml(n.sourceDiscussionId || '')}">
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
        } catch (err) {
            console.warn('[Auth Navbar] Failed to load notifications:', err);
            list.innerHTML = '<p class="text-sm text-error text-center py-4">Failed to load</p>';
        }
    }

    async function handleNotificationClick(element: HTMLElement): Promise<void> {
        const notificationId = element.dataset.notificationId;
        const discussionId = element.dataset.discussionId;

        if (!notificationId) return;

        try {
            await fetch(`/bff/notifications/${notificationId}/read`, {
                method: 'POST',
                credentials: 'include'
            });

            loadNotificationCount();

            const item = document.querySelector(`[data-id="${notificationId}"]`);
            if (item) item.classList.remove('unread');

            if (discussionId) {
                // Navigate to discussion if available
            }
        } catch (err) {
            console.warn('[Auth Navbar] Failed to mark notification as read:', err);
        }
    }

    async function markAllNotificationsAsRead(): Promise<void> {
        try {
            await fetch('/bff/notifications/read-all', {
                method: 'POST',
                credentials: 'include'
            });

            updateNotificationBadge(0);

            document.querySelectorAll('.notification-item.unread').forEach(el => {
                el.classList.remove('unread');
            });
        } catch (err) {
            console.warn('[Auth Navbar] Failed to mark all notifications as read:', err);
        }
    }

    async function handleLogout(): Promise<void> {
        try {
            await fetch('/bff/auth/logout', {
                method: 'POST',
                credentials: 'include'
            });
        } catch (err) {
            console.warn('[Auth Navbar] Logout error:', err);
        } finally {
            window.location.replace('/');
        }
    }

    // ===== Helper Functions =====

    function getNotificationIconClass(type: string): string {
        const classes: Record<string, string> = {
            'Reply': 'text-primary',
            'Mention': 'text-accent'
        };
        return classes[type] || 'text-muted';
    }

    function getNotificationIcon(type: string): string {
        const icons: Record<string, string> = {
            'Reply': '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" /></svg>',
            'Mention': '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 12a4 4 0 10-8 0 4 4 0 008 0zm0 0v1.5a2.5 2.5 0 005 0V12a9 9 0 10-9 9m4.5-1.206a8.959 8.959 0 01-4.5 1.207" /></svg>',
            'NewPostInFollowedDiscussion': '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 8h10M7 12h4m1 8l-4-4H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-3l-4 4z" /></svg>'
        };
        return icons[type] || '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" /></svg>';
    }

    function formatTimeAgo(dateString: string): string {
        const date = new Date(dateString);
        const now = new Date();
        const diffMs = now.getTime() - date.getTime();
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);

        if (diffMins < 1) return 'just now';
        if (diffMins < 60) return `${diffMins}m ago`;
        if (diffHours < 24) return `${diffHours}h ago`;
        if (diffDays < 7) return `${diffDays}d ago`;
        const tz = (window as any).snakkTimezone || 'UTC';
        try {
            return date.toLocaleDateString('en-US', { timeZone: tz, month: 'short', day: 'numeric' });
        } catch {
            return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
        }
    }

    function escapeHtml(text: string): string {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // ===== Event Delegation =====

    document.addEventListener('click', async (event) => {
        const target = event.target as HTMLElement;
        const action = target.closest('[data-action]') as HTMLElement | null;
        if (!action) return;

        event.preventDefault();

        const actionName = action.dataset.action;

        switch (actionName) {
            case 'logout':
                await handleLogout();
                break;
            case 'toggle-theme':
                (window as any).snakkTheme?.toggleTheme();
                break;
            case 'mark-all-notifications-read':
                await markAllNotificationsAsRead();
                break;
            case 'click-notification':
                await handleNotificationClick(action);
                break;
        }
    });

    // Close daisyUI dropdowns when any item inside them is clicked.
    // Without this, HTMX-boosted links and data-action anchors never cause
    // the trigger label to lose focus, so the dropdown stays open.
    document.addEventListener('click', (event) => {
        const target = event.target as HTMLElement;
        if (target.closest('.dropdown-content')) {
            (document.activeElement as HTMLElement)?.blur();
        }
    });

    // ===== Listen for Realtime Events =====

    document.addEventListener('snakk:realtime:notification-count', (event) => {
        const customEvent = event as CustomEvent<{ unreadCount: number }>;
        updateNotificationBadge(customEvent.detail.unreadCount);
    });

    document.addEventListener('snakk:realtime:notification', () => {
        loadNotificationCount();
        loadNotifications();
    });

    // ===== Initialize =====

    function init(): void {
        // Only load notifications if authenticated (server sets window.currentUserId)
        if ((window as any).currentUserId) {
            loadNotificationCount();
            loadNotifications();
        }

        // Update theme toggle button icon with current state
        (window as any).snakkTheme?.updateToggleButton();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Export minimal API
    (window as any).SnakkAuthNav = {
        updateNotificationBadge
    };
})();
