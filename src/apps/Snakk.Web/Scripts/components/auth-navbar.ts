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
        document.querySelectorAll<HTMLElement>('.sn-notification-badge').forEach(badge => {
            if (count > 0) {
                badge.textContent = count > 99 ? '99+' : count.toString();
                badge.classList.remove('sn-hidden');
            } else {
                badge.classList.add('sn-hidden');
            }
        });
    }

    async function loadNotifications(): Promise<void> {
        const lists = document.querySelectorAll<HTMLElement>('.sn-notification-list');
        if (!lists.length) return;

        try {
            const response = await fetch('/bff/notifications?offset=0&pageSize=10', { credentials: 'include' });
            const data: NotificationsResponse = await response.json();

            if (!data.items || data.items.length === 0) {
                lists.forEach(list => { list.innerHTML = '<p class="text-sm text-muted text-center py-4">No notifications yet</p>'; });
                return;
            }

            const html = data.items.map(n => `
                <div class="sn-notification-item ${n.isRead ? '' : 'sn-unread'}" data-id="${escapeHtml(n.publicId)}">
                    <div class="sn-flex sn-items-start sn-gap-2 sn-p-2 sn-rounded hover:bg-subtle sn-cursor-pointer"
                         data-action="click-notification"
                         data-notification-id="${escapeHtml(n.publicId)}"
                         data-discussion-id="${escapeHtml(n.sourceDiscussionId || '')}">
                        <div class="sn-notification-icon ${getNotificationIconClass(n.type)}">
                            ${getNotificationIcon(n.type)}
                        </div>
                        <div class="sn-flex-1 sn-min-w-0">
                            <p class="sn-text-sm sn-font-medium sn-truncate">${escapeHtml(n.title)}</p>
                            ${n.body ? `<p class="sn-text-xs sn-text-muted line-clamp-2">${escapeHtml(n.body)}</p>` : ''}
                            <p class="sn-text-xs sn-text-muted sn-mt-1">${formatTimeAgo(n.createdAt)}</p>
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
            if (item) item.classList.remove('sn-unread');

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

            document.querySelectorAll('.sn-notification-item.sn-unread').forEach(el => {
                el.classList.remove('sn-unread');
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
            window.SnakkBroadcast?.send({ type: 'auth:logout' });
            window.location.replace('/');
        }
    }

    // ===== Helper Functions =====

    function getNotificationIconClass(type: string): string {
        const classes: Record<string, string> = {
            'Reply': 'sn-text-primary',
            'Mention': 'text-accent'
        };
        return classes[type] || 'sn-text-muted';
    }

    function getNotificationIcon(type: string): string {
        const icons: Record<string, string> = {
            'Reply': '<span class="sn-icon icon-reply sn-h-4 sn-w-4" aria-hidden="true"></span>',
            'Mention': '<span class="sn-icon icon-at-sign sn-h-4 sn-w-4" aria-hidden="true"></span>',
            'NewPostInFollowedDiscussion': '<span class="sn-icon icon-chat-alt sn-h-4 sn-w-4" aria-hidden="true"></span>'
        };
        return icons[type] || '<span class="sn-icon icon-bell sn-h-4 sn-w-4" aria-hidden="true"></span>';
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
                break;
            case 'mark-all-notifications-read':
                await markAllNotificationsAsRead();
                break;
            case 'click-notification':
                await handleNotificationClick(action);
                break;
        }
    });

    // Close a popover panel when any interactive item inside it is clicked.
    // The Popover API handles light-dismiss (click outside) for free; this covers
    // clicks on links, buttons, and data-action elements inside the panel.
    document.addEventListener('click', (event) => {
        const target = event.target as HTMLElement;
        const panel = target.closest<HTMLElement>('[popover].sn-dropdown-panel');
        if (!panel || !panel.matches(':popover-open')) return;
        if (target.closest('a, button, [data-action]')) {
            panel.hidePopover();
        }
    });

    // ===== Dropdown Positioning =====

    function positionPanel(panel: HTMLElement): void {
        const trigger = document.querySelector<HTMLElement>(`[popovertarget="${panel.id}"]`);
        if (!trigger) return;
        const rect = trigger.getBoundingClientRect();
        if (panel.classList.contains('sn-dropdown-panel-top')) {
            panel.style.top = 'auto';
            panel.style.bottom = `${window.innerHeight - rect.top + 4}px`;
        } else {
            panel.style.top = `${rect.bottom + 4}px`;
            panel.style.bottom = 'auto';
        }
        panel.style.right = `${window.innerWidth - rect.right}px`;
        panel.style.left = 'auto';
    }

    function initDropdownPositioning(root: Document | Element = document): void {
        root.querySelectorAll<HTMLElement>('[popover].sn-dropdown-panel:not([data-dp-init])').forEach(panel => {
            panel.dataset.dpInit = '1';
            panel.addEventListener('beforetoggle', (e: Event) => {
                if ((e as ToggleEvent).newState === 'open') {
                    positionPanel(panel);
                }
            });
        });
    }

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
        document.querySelectorAll<HTMLElement>('.sn-dm-badge').forEach(badge => {
            if (count > 0) {
                badge.textContent = count > 99 ? '99+' : count.toString();
                badge.classList.remove('sn-hidden');
            } else {
                badge.classList.add('sn-hidden');
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
            if (document.body.dataset.messaging === '1') {
                loadDmUnreadCount();
            }
        }

        // Update theme toggle button icon with current state
        (window as any).snakkTheme?.updateToggleButton();

        // Wire up Popover API positioning for all dropdown panels on this page
        initDropdownPositioning();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    document.addEventListener('htmx:afterSettle', () => initDropdownPositioning());

    // Cross-tab sync via BroadcastChannel
    window.SnakkBroadcast?.on('auth:logout', () => { window.location.replace('/'); });
    window.SnakkBroadcast?.on('profile:avatar-changed', msg => {
        document.querySelectorAll<HTMLImageElement>(
            '#auth-nav .avatar-sm img, #auth-nav .sn-user-menu-card-avatar, .sn-tabbar .avatar-sm img, .sn-tabbar .sn-user-menu-card-avatar'
        ).forEach(img => { img.removeAttribute('srcset'); img.src = msg.url; });
    });
    window.SnakkBroadcast?.on('profile:display-name-changed', msg => {
        document.querySelectorAll<HTMLElement>('.sn-user-menu-card-name').forEach(el => { el.textContent = msg.displayName; });
    });
    window.SnakkBroadcast?.on('pref:content-width-changed', msg => {
        if (msg.value === 54) document.documentElement.style.removeProperty('--content-width');
        else document.documentElement.style.setProperty('--content-width', msg.value + 'rem');
    });

    // ============================================================================
    // Mod Panel
    // ============================================================================

    interface ModEntity {
        role: string;
        communityPublicId?: string;
        communityName?: string;
        hubPublicId?: string;
        hubName?: string;
        spacePublicId?: string;
        spaceName?: string;
    }

    let modPanelLoaded = false;

    function escModText(s: string): string {
        return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    async function loadModPanel(): Promise<void> {
        if (modPanelLoaded) return;
        const body = document.getElementById('mod-panel-body');
        if (!body) return;
        try {
            const res = await fetch('/bff/me/moderated-entities');
            if (!res.ok) throw new Error('Failed to load');
            const data: { entities: ModEntity[] } = await res.json();
            modPanelLoaded = true;
            if (!data.entities || data.entities.length === 0) {
                body.innerHTML = '<p class="sn-text-sm sn-text-muted">No moderated spaces found.</p>';
                return;
            }
            const items = data.entities.map(e => {
                const name = escModText(e.spaceName || e.hubName || e.communityName || 'Unknown');
                const scope = e.spaceName ? 'Space' : e.hubName ? 'Hub' : e.communityName ? 'Community' : 'Platform';
                const roleLabel = escModText(e.role.replace(/([A-Z])/g, ' $1').trim());
                return `<div class="sn-mod-panel-item">
                    <span class="sn-text-sm sn-font-medium">${name}</span>
                    <span class="sn-text-xs sn-text-muted">${scope} · ${roleLabel}</span>
                </div>`;
            }).join('');
            body.innerHTML = `<div class="sn-mod-panel-list">${items}</div>
                <div class="sn-mt-3 sn-border-t sn-border-subtle sn-pt-2">
                    <a href="/moderation" class="sn-btn sn-btn-sm sn-btn-outline sn-w-full sn-justify-center" hx-boost="false">Open moderation panel</a>
                </div>`;
        } catch {
            body.innerHTML = '<p class="sn-text-sm sn-text-error">Failed to load.</p>';
        }
    }

    const modShieldBtn = document.getElementById('mod-shield-btn');
    if (modShieldBtn) {
        modShieldBtn.addEventListener('click', loadModPanel);
    }

    // Export minimal API
    (window as any).SnakkAuthNav = {
        updateNotificationBadge,
        updateDmBadge
    };

    // Export dropdown positioning so dynamically-created panels (e.g. discussion-detail.ts)
    // can register toggle listeners after inserting new panels into the DOM.
    (window as any).snakkDropdown = {
        position: positionPanel,
        init: initDropdownPositioning
    };
})();
