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
        document.querySelectorAll<HTMLElement>('.notification-badge').forEach(badge => {
            if (count > 0) {
                badge.textContent = count > 99 ? '99+' : count.toString();
                badge.classList.remove('hidden');
            } else {
                badge.classList.add('hidden');
            }
        });
    }

    async function loadNotifications(): Promise<void> {
        const lists = document.querySelectorAll<HTMLElement>('.notification-list');
        if (!lists.length) return;

        try {
            const response = await fetch('/bff/notifications?offset=0&pageSize=10', { credentials: 'include' });
            const data: NotificationsResponse = await response.json();

            if (!data.items || data.items.length === 0) {
                lists.forEach(list => { list.innerHTML = '<p class="text-sm text-muted text-center py-4">No notifications yet</p>'; });
                return;
            }

            const html = data.items.map(n => `
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
            lists.forEach(list => { list.innerHTML = html; });
        } catch (err) {
            console.warn('[Auth Navbar] Failed to load notifications:', err);
            lists.forEach(list => { list.innerHTML = '<p class="text-sm text-error text-center py-4">Failed to load</p>'; });
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
            'Reply': '<span class="icon icon-reply h-4 w-4" aria-hidden="true"></span>',
            'Mention': '<span class="icon icon-at-sign h-4 w-4" aria-hidden="true"></span>',
            'NewPostInFollowedDiscussion': '<span class="icon icon-chat-alt h-4 w-4" aria-hidden="true"></span>'
        };
        return icons[type] || '<span class="icon icon-bell h-4 w-4" aria-hidden="true"></span>';
    }

    const formatTimeAgo = (dateString: string): string => (window as any).SnakkUtils.formatRelativeTime(dateString);

    const escapeHtml = (text: string): string => (window as any).SnakkUtils.escapeHtml(text);

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
                (document.activeElement as HTMLElement)?.blur();
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

    // ===== DM Badge =====

    async function loadDmUnreadCount(): Promise<void> {
        try {
            const response = await fetch('/bff/messages/unread-count', { credentials: 'include' });
            if (!response.ok) return;
            const data: { count: number } = await response.json();
            updateDmBadge(data.count);
        } catch {
            // Feature may be disabled or user not authenticated — silent failure is correct
        }
    }

    function updateDmBadge(count: number): void {
        document.querySelectorAll<HTMLElement>('.dm-badge').forEach(badge => {
            if (count > 0) {
                badge.textContent = count > 99 ? '99+' : count.toString();
                badge.classList.remove('hidden');
            } else {
                badge.classList.add('hidden');
            }
        });
    }

    // ===== Listen for Realtime Events =====

    document.addEventListener('snakk:realtime:notification-count', (event) => {
        const customEvent = event as CustomEvent<{ unreadCount: number }>;
        updateNotificationBadge(customEvent.detail.unreadCount);
    });

    document.addEventListener('snakk:realtime:notification', () => {
        loadNotificationCount();
        loadNotifications();
    });

    document.addEventListener('snakk:realtime:dm-count', (event) => {
        const customEvent = event as CustomEvent<{ unreadCount: number }>;
        updateDmBadge(customEvent.detail.unreadCount);
    });

    // ===== Initialize =====

    function init(): void {
        // Only load notifications if authenticated (server sets window.currentUserId)
        if ((window as any).currentUserId) {
            loadNotificationCount();
            loadNotifications();
            loadDmUnreadCount();
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
        updateNotificationBadge,
        updateDmBadge
    };
})();
