(function(): void {
    'use strict';

    const COUNT_KEY = 'snakk:unreadCount';
    const LAST_RESPONSE_KEY = 'snakk:unreadLastResponse';

    function setBadge(badge: HTMLElement, count: number): void {
        if (count > 0) {
            badge.textContent = count > 99 ? '99+' : String(count);
            badge.classList.remove('sn-hidden');
        } else {
            badge.textContent = '';
            badge.classList.add('sn-hidden');
        }
    }

    function updateActiveLink(): void {
        const path = location.pathname;
        document.querySelectorAll<HTMLAnchorElement>('.sn-nav .sn-nav-item').forEach(a => {
            const href = a.getAttribute('href') ?? '';
            let active: boolean;
            if (href.endsWith('/rules')) active = path.endsWith('/rules');
            else if (href.endsWith('/moderators')) active = path.endsWith('/moderators');
            else active = path === href;
            a.classList.toggle('sn-active', active);
        });
    }

    async function loadUnreadFeedCount(): Promise<void> {
        const badge = document.getElementById('unread-feed-badge');
        if (!badge) return;

        const storedStr = localStorage.getItem(COUNT_KEY);
        const displayCount = storedStr !== null ? parseInt(storedStr, 10) : null;
        if (displayCount !== null) setBadge(badge, displayCount);

        try {
            const res = await fetch('/bff/me/unread-count', { credentials: 'include' });
            if (!res.ok) return;
            const data: { count: number } = await res.json();
            const serverCount = data.count;

            const lastStr = localStorage.getItem(LAST_RESPONSE_KEY);
            const lastResponse = lastStr !== null ? parseInt(lastStr, 10) : null;

            let finalCount: number;
            if (displayCount !== null && displayCount !== serverCount && serverCount === lastResponse) {
                // Server returned the same value as last time — treat as stale, local override wins
                finalCount = displayCount;
            } else {
                finalCount = serverCount;
                localStorage.setItem(COUNT_KEY, String(finalCount));
            }

            localStorage.setItem(LAST_RESPONSE_KEY, String(serverCount));
            setBadge(badge, finalCount);
        } catch {
            // ignore
        }
    }

    document.addEventListener('htmx:afterRequest', function(evt: Event): void {
        const detail = (evt as CustomEvent).detail;
        if (detail?.elt?.id === 'unread-mark-read-btn' && detail?.successful) {
            document.dispatchEvent(new CustomEvent('snakk:unread:cleared'));
            location.reload();
        }
    });

    document.addEventListener('snakk:unread:cleared', function(): void {
        localStorage.setItem(COUNT_KEY, '0');
        const badge = document.getElementById('unread-feed-badge');
        if (badge) setBadge(badge, 0);
    });

    document.addEventListener('DOMContentLoaded', loadUnreadFeedCount);

    document.addEventListener('htmx:afterSwap', function(evt: Event): void {
        const detail = (evt as CustomEvent).detail;
        if (detail?.target?.id === 'main-content') {
            updateActiveLink();
            loadUnreadFeedCount();
        }
    });
})();
