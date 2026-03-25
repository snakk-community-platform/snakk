/**
 * SignalR Realtime Connection Manager
 *
 * Uses a SharedWorker (one SignalR connection across all tabs) with a fallback
 * to a direct per-tab connection for browsers that don't support SharedWorker.
 *
 * Auth: fetches a short-lived JWT from /bff/realtime-token (cookie-backed BFF).
 * Subscriptions: reads publicIds from #page-context data attributes — never slugs.
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface RealtimeMessage {
    group?: string;
    eventType: string;
    targetId: string;
    htmlContent: string;
    swapStrategy: 'beforeend' | 'afterbegin' | 'innerHTML' | 'outerHTML';
    postId?: string;
    counts?: ReactionCounts;
    discussionId?: string;
    title?: string;
    delta?: number;
    authorId?: string;
    authorName?: string;
}

interface ReactionCounts {
    ThumbsUp?: number;
    Heart?: number;
    Eyes?: number;
    Crazy?: number;
    [key: string]: number | undefined;
}

interface ViewerCountData {
    count: number;
    group?: string;
}

interface TypingData {
    displayName: string;
    isTyping: boolean;
    group?: string;
}

interface NotificationData {
    unreadCount: number;
}

interface Notification {
    type: string;
    [key: string]: any;
}

interface PageContext {
    discussionId: string | null;
    spaceId: string | null;
    hubId: string | null;
}

interface Subscriptions {
    discussionId: string | null;
    spaceId: string | null;
    hubId: string | null;
}

// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    const sanitizeHtml = (window as any).SnakkUtils?.sanitizeHtml || function(html: string): string {
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

    const snakkDebug = (window as any).SnakkDebug;
    function debugLog(message: string): void {
        if (snakkDebug?.log) snakkDebug.log('SignalR', message);
        else console.debug('[Realtime]', message);
    }

    // ============================================================================
    // Token management (shared between worker and direct paths)
    // ============================================================================

    let cachedToken: string | null = null;
    let tokenExpiry = 0;

    async function fetchToken(): Promise<string | null> {
        if (cachedToken && Date.now() < tokenExpiry - 60_000) return cachedToken;
        try {
            const resp = await fetch('/bff/realtime-token', { credentials: 'include' });
            if (!resp.ok) return null;
            const data = await resp.json();
            cachedToken = data.token;
            tokenExpiry = Date.now() + data.expiresInSeconds * 1000;
            return cachedToken;
        } catch {
            return null;
        }
    }

    // ============================================================================
    // Page context — read publicIds from #page-context data attributes
    // ============================================================================

    function getPageContext(): PageContext {
        const el = document.getElementById('page-context');
        return {
            discussionId: el?.dataset.discussionId || null,
            spaceId: el?.dataset.spaceId || null,
            hubId: el?.dataset.hubId || null,
        };
    }

    // ============================================================================
    // Realtime event handlers
    // ============================================================================

    function isNearBottom(threshold = 200): boolean {
        return (window.scrollY + window.innerHeight) >= (document.documentElement.scrollHeight - threshold);
    }

    function showNewPostIndicator(): void {
        let indicator = document.getElementById('new-post-indicator');
        if (!indicator) {
            indicator = document.createElement('button');
            indicator.id = 'new-post-indicator';
            indicator.className = 'fixed bottom-24 right-6 btn btn-primary btn-sm shadow-lg z-50 animate-bounce';
            indicator.innerHTML = '↓ New post';
            indicator.onclick = function() {
                const posts = document.querySelectorAll<HTMLElement>('[id^="post-"]');
                if (posts.length > 0) posts[posts.length - 1]?.scrollIntoView({ behavior: 'smooth', block: 'center' });
                indicator?.remove();
            };
            document.body.appendChild(indicator);
        }
    }

    function handleReactionUpdate(postId: string, counts: ReactionCounts): void {
        const reactionsBar = document.getElementById(`reactions-${postId}`);
        if (!reactionsBar) return;

        const reactionEmojis: Record<string, string> = { ThumbsUp: '👍', Heart: '❤️', Eyes: '👀', Crazy: '🤯' };
        const dataKeys: Record<string, string> = { ThumbsUp: 'thumbsup', Heart: 'heart', Eyes: 'eyes', Crazy: 'crazy' };

        let html = '';
        for (const [type, emoji] of Object.entries(reactionEmojis)) {
            const count = counts[type] || 0;
            reactionsBar.setAttribute(`data-count-${dataKeys[type]}`, String(count));
            if (count > 0) html += `<span data-type="${type}">${emoji} ${count}</span>`;
        }

        if (!html) {
            html = '<span class="hidden group-hover:inline" data-reaction-placeholder><svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14.828 14.828a4 4 0 01-5.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg></span>';
        }

        reactionsBar.innerHTML = html;
    }

    function handleDiscussionLockChange(isLocked: boolean): void {
        document.getElementById('discussion-lock-banner')?.classList.toggle('hidden', !isLocked);
        document.getElementById('reply-form')?.classList.toggle('hidden', isLocked);
    }

    function handleDiscussionPinned(discussionId: string, isPinned: boolean): void {
        const item = document.querySelector<HTMLElement>(`.topic-item-wrapper[data-discussion-id="${CSS.escape(discussionId)}"]`);
        if (!item) return;
        item.classList.toggle('is-pinned', isPinned);
        // Move pinned items to top, unpinned back to natural order
        const container = item.parentElement;
        if (!container) return;
        if (isPinned) container.prepend(item);
    }

    function handleDiscussionDeleted(discussionId: string): void {
        document.querySelector(`.topic-item-wrapper[data-discussion-id="${CSS.escape(discussionId)}"]`)?.remove();
    }

    function handleDiscussionTitleUpdated(title: string): void {
        // Update the <h1> on the discussion detail page
        const h1 = document.querySelector<HTMLElement>('h1.discussion-title');
        if (h1) h1.textContent = title;
        // Update browser tab title (keep the suffix after " — ")
        const titleParts = document.title.split(' — ');
        if (titleParts.length > 1) document.title = `${title} — ${titleParts.slice(1).join(' — ')}`;
        else document.title = title;
    }

    function handlePostCountUpdated(discussionId: string, delta: number): void {
        const item = document.querySelector<HTMLElement>(`.topic-item-wrapper[data-discussion-id="${CSS.escape(discussionId)}"]`);
        if (!item) return;
        const countEl = item.querySelector<HTMLElement>('[data-stat="post-count"]');
        if (!countEl) return;
        const current = parseInt(countEl.textContent || '0', 10);
        if (!isNaN(current)) countEl.textContent = String(current + delta);
    }

    function handleReadStateUpdated(discussionId: string): void {
        document.dispatchEvent(new CustomEvent('snakk:realtime:read-state-updated', {
            detail: { discussionId }
        }));
    }

    function handleAnnouncementUpdated(): void {
        document.dispatchEvent(new CustomEvent('snakk:realtime:announcement-updated'));
    }

    async function handleDiscussionCreated(discussionId: string, authorId: string, authorName: string): Promise<void> {
        const container = document.getElementById('discussions-container');
        if (!container) return;

        const ctx = document.getElementById('page-context');
        const hubSlug = ctx?.dataset.hubSlug;
        const spaceSlug = ctx?.dataset.spaceSlug;
        if (!hubSlug || !spaceSlug) return;

        try {
            const params = new URLSearchParams({ discussionId, hubSlug, spaceSlug, authorId, authorName });
            const resp = await fetch(`/partials/discussion-item?${params}`);
            if (!resp.ok) return;

            const safeHtml = sanitizeHtml(await resp.text());
            container.insertAdjacentHTML('afterbegin', safeHtml);

            if (typeof htmx !== 'undefined') htmx.process(container);
        } catch { /* silently ignore fetch failures */ }
    }

    async function handlePostCreated(postId: string, discussionId: string): Promise<void> {
        const container = document.getElementById('posts-container');
        if (!container) return;

        try {
            const resp = await fetch(
                `/partials/post?discussionId=${encodeURIComponent(discussionId)}&postId=${encodeURIComponent(postId)}`
            );
            if (!resp.ok) return;

            const safeHtml = sanitizeHtml(await resp.text());
            const wasNearBottom = isNearBottom();
            container.insertAdjacentHTML('beforeend', safeHtml);

            const newEl = container.lastElementChild as HTMLElement | null;
            if (newEl) {
                newEl.classList.add('post-new');
                setTimeout(() => newEl.classList.remove('post-new'), 500);
                if (wasNearBottom) newEl.scrollIntoView({ behavior: 'smooth', block: 'end' });
                else showNewPostIndicator();
            }

            if (typeof htmx !== 'undefined') htmx.process(container);
        } catch { /* silently ignore fetch failures */ }
    }

    async function handlePostEdited(postId: string, discussionId: string): Promise<void> {
        const target = document.querySelector(`[data-post-id="${CSS.escape(postId)}"]`) as HTMLElement | null;
        if (!target) return;

        try {
            const resp = await fetch(
                `/partials/post?discussionId=${encodeURIComponent(discussionId)}&postId=${encodeURIComponent(postId)}`
            );
            if (!resp.ok) return;

            const safeHtml = sanitizeHtml(await resp.text());
            target.outerHTML = safeHtml;

            if (typeof htmx !== 'undefined') htmx.process(document.body);
        } catch { /* silently ignore fetch failures */ }
    }

    function handleViewerCount(data: ViewerCountData): void {
        const el = document.getElementById('viewer-count');
        if (!el) return;
        if (data.count > 1) {
            el.textContent = `${data.count} viewing`;
            el.classList.remove('hidden');
        } else {
            el.classList.add('hidden');
        }
    }

    const typingUsers = new Map<string, ReturnType<typeof setTimeout>>();

    function handleTypingIndicator(data: TypingData): void {
        if (data.isTyping) {
            const existing = typingUsers.get(data.displayName);
            if (existing) clearTimeout(existing);
            typingUsers.set(data.displayName, setTimeout(() => {
                typingUsers.delete(data.displayName);
                renderTypingIndicator();
            }, 3000));
        } else {
            const existing = typingUsers.get(data.displayName);
            if (existing) clearTimeout(existing);
            typingUsers.delete(data.displayName);
        }
        renderTypingIndicator();
    }

    function renderTypingIndicator(): void {
        const el = document.getElementById('typing-indicator');
        const usersEl = document.getElementById('typing-users');
        if (!el || !usersEl) return;

        const names = Array.from(typingUsers.keys());
        if (names.length === 0) {
            el.classList.add('hidden');
        } else if (names.length === 1) {
            usersEl.textContent = `${names[0]} is typing...`;
            el.classList.remove('hidden');
        } else if (names.length === 2) {
            usersEl.textContent = `${names[0]} and ${names[1]} are typing...`;
            el.classList.remove('hidden');
        } else {
            usersEl.textContent = `${names.length} people are typing...`;
            el.classList.remove('hidden');
        }
    }

    function handleReceiveUpdate(message: RealtimeMessage): void {
        debugLog(`Update: ${message.eventType}`);

        if (message.eventType === 'reaction-updated' && message.postId && message.counts) {
            handleReactionUpdate(message.postId, message.counts);
            return;
        }
        if (message.eventType === 'discussion-locked') { handleDiscussionLockChange(true); return; }
        if (message.eventType === 'discussion-unlocked') { handleDiscussionLockChange(false); return; }
        if (message.eventType === 'discussion-created' && message.discussionId && message.authorId && message.authorName) {
            handleDiscussionCreated(message.discussionId, message.authorId, message.authorName);
            return;
        }
        if (message.eventType === 'discussion-pinned' && message.discussionId) {
            handleDiscussionPinned(message.discussionId, true);
            return;
        }
        if (message.eventType === 'discussion-unpinned' && message.discussionId) {
            handleDiscussionPinned(message.discussionId, false);
            return;
        }
        if (message.eventType === 'discussion-deleted' && message.discussionId) {
            handleDiscussionDeleted(message.discussionId);
            return;
        }
        if (message.eventType === 'discussion-title-updated' && message.title) {
            handleDiscussionTitleUpdated(message.title);
            return;
        }
        if (message.eventType === 'post-count-updated' && message.discussionId && message.delta != null) {
            handlePostCountUpdated(message.discussionId, message.delta);
            return;
        }
        if (message.eventType === 'read-state-updated' && message.discussionId) {
            handleReadStateUpdated(message.discussionId);
            return;
        }
        if (message.eventType === 'announcement-updated') {
            handleAnnouncementUpdated();
            return;
        }
        if (message.eventType === 'global-announcement' && !document.hidden) {
            document.dispatchEvent(new CustomEvent('snakk:realtime:global-announcement', {
                detail: { message: message.htmlContent }
            }));
            return;
        }

        if (message.eventType === 'post-created' && message.postId && message.discussionId) {
            handlePostCreated(message.postId, message.discussionId);
            return;
        }

        if (message.eventType === 'post-edited' && message.postId && message.discussionId) {
            handlePostEdited(message.postId, message.discussionId);
            return;
        }

        const target = document.getElementById(message.targetId);
        if (!target) { debugLog(`Target not found: ${message.targetId}`); return; }

        const safeHtml = sanitizeHtml(message.htmlContent);

        switch (message.swapStrategy) {
            case 'beforeend': target.insertAdjacentHTML('beforeend', safeHtml); break;
            case 'afterbegin': target.insertAdjacentHTML('afterbegin', safeHtml); break;
            case 'innerHTML':  target.innerHTML = safeHtml; break;
            case 'outerHTML':
                if (message.htmlContent === '') target.remove();
                else target.outerHTML = safeHtml;
                break;
            default: target.innerHTML = safeHtml;
        }

        if (typeof htmx !== 'undefined') htmx.process(target.parentElement || document.body);
    }

    function handleNotificationCount(data: NotificationData): void {
        document.dispatchEvent(new CustomEvent('snakk:realtime:notification-count', {
            detail: { unreadCount: data.unreadCount }
        }));
    }

    function handleNotification(notification: Notification): void {
        document.dispatchEvent(new CustomEvent('snakk:realtime:notification', {
            detail: { notification }
        }));
    }

    // ============================================================================
    // SharedWorker path
    // ============================================================================

    let worker: SharedWorker | null = null;
    const currentSubs: Subscriptions = { discussionId: null, spaceId: null, hubId: null };

    function trySharedWorker(): boolean {
        if (typeof SharedWorker === 'undefined') return false;
        try {
            const meta = document.querySelector<HTMLMetaElement>('meta[name="signalr-src"]');
            if (!meta) return false;

            worker = new SharedWorker('/js/dist/services/realtime-worker.js');
            worker.port.start();

            // Send init with the realtime URL and SignalR script URL
            const realtimeUrl = (window as any).realtimeServiceUrl || 'https://localhost:17101/realtime';
            worker.port.postMessage({ type: 'init', realtimeUrl, signalrSrc: meta.content });

            worker.port.onmessage = (e: MessageEvent) => {
                const msg = e.data;
                if (msg.type === 'message') {
                    switch (msg.event) {
                        case 'ReceiveUpdate':          handleReceiveUpdate(msg.data); break;
                        case 'ReceiveViewerCount':     handleViewerCount(msg.data); break;
                        case 'ReceiveTyping':          handleTypingIndicator(msg.data); break;
                        case 'ReceiveNotificationCount': handleNotificationCount(msg.data); break;
                        case 'ReceiveNotification':    handleNotification(msg.data); break;
                    }
                } else if (msg.type === 'connection-state') {
                    debugLog(`Worker connection: ${msg.state}`);
                }
            };

            worker.onerror = () => {
                debugLog('SharedWorker error — falling back to direct connection');
                worker = null;
                startDirectConnection();
            };

            updateWorkerSubscriptions();
            return true;
        } catch {
            return false;
        }
    }

    function updateWorkerSubscriptions(): void {
        if (!worker) return;
        const ctx = getPageContext();

        // Discussion
        if (currentSubs.discussionId && currentSubs.discussionId !== ctx.discussionId)
            worker.port.postMessage({ type: 'unsubscribe', group: `discussion:${currentSubs.discussionId}` });
        if (ctx.discussionId && ctx.discussionId !== currentSubs.discussionId)
            worker.port.postMessage({ type: 'subscribe', group: `discussion:${ctx.discussionId}` });
        currentSubs.discussionId = ctx.discussionId;

        // Space
        if (currentSubs.spaceId && currentSubs.spaceId !== ctx.spaceId)
            worker.port.postMessage({ type: 'unsubscribe', group: `space:${currentSubs.spaceId}` });
        if (ctx.spaceId && ctx.spaceId !== currentSubs.spaceId)
            worker.port.postMessage({ type: 'subscribe', group: `space:${ctx.spaceId}` });
        currentSubs.spaceId = ctx.spaceId;

        // Hub
        if (currentSubs.hubId && currentSubs.hubId !== ctx.hubId)
            worker.port.postMessage({ type: 'unsubscribe', group: `hub:${currentSubs.hubId}` });
        if (ctx.hubId && ctx.hubId !== currentSubs.hubId)
            worker.port.postMessage({ type: 'subscribe', group: `hub:${ctx.hubId}` });
        currentSubs.hubId = ctx.hubId;
    }

    // ============================================================================
    // Direct connection path (fallback)
    // ============================================================================

    let connection: signalR.HubConnection | null = null;
    const directSubs: Subscriptions = { discussionId: null, spaceId: null, hubId: null };

    function loadSignalR(): Promise<void> {
        return new Promise((resolve, reject) => {
            if (typeof signalR !== 'undefined') { resolve(); return; }
            const meta = document.querySelector('meta[name="signalr-src"]');
            if (!meta) { reject(new Error('SignalR meta tag not found')); return; }
            const script = document.createElement('script');
            script.src = meta.getAttribute('content')!;
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('Failed to load SignalR'));
            document.body.appendChild(script);
        });
    }

    function startDirectConnection(): void {
        const realtimeUrl = (window as any).realtimeServiceUrl || 'https://localhost:17101/realtime';

        loadSignalR()
            .then(async () => {
                const token = await fetchToken();
                if (!token) {
                    debugLog('No auth token — realtime disabled');
                    return;
                }

                connection = new signalR.HubConnectionBuilder()
                    .withUrl(realtimeUrl, { accessTokenFactory: fetchToken as () => Promise<string> })
                    .withAutomaticReconnect([0, 2000, 10000, 30000])
                    .build();

                connection.on('ReceiveUpdate', handleReceiveUpdate);
                connection.on('ReceiveViewerCount', handleViewerCount);
                connection.on('ReceiveTyping', handleTypingIndicator);
                connection.on('ReceiveNotificationCount', handleNotificationCount);
                connection.on('ReceiveNotification', handleNotification);

                connection.onreconnected(() => { debugLog('Reconnected'); subscribeToGroups(); });
                connection.onreconnecting(() => debugLog('Reconnecting...'));
                connection.onclose(() => debugLog('Disconnected'));

                (window as any).snakkRealtime = connection;

                return connection.start();
            })
            .then(() => {
                debugLog('Connected (direct)');
                subscribeToGroups();
            })
            .catch(() => debugLog('Connection error'));
    }

    function subscribeToGroups(): void {
        if (!connection) return;
        connection.invoke('SubscribeToGlobal').catch(() => {});

        const ctx = getPageContext();
        if (ctx.discussionId) {
            connection.invoke('SubscribeToDiscussion', ctx.discussionId).catch(() => {});
            directSubs.discussionId = ctx.discussionId;
        }
        if (ctx.spaceId) {
            connection.invoke('SubscribeToSpace', ctx.spaceId).catch(() => {});
            directSubs.spaceId = ctx.spaceId;
        }
        if (ctx.hubId) {
            connection.invoke('SubscribeToHub', ctx.hubId).catch(() => {});
            directSubs.hubId = ctx.hubId;
        }
    }

    function updateDirectSubscriptions(): void {
        if (!connection) return;
        const ctx = getPageContext();

        if (directSubs.discussionId && directSubs.discussionId !== ctx.discussionId) {
            connection.invoke('UnsubscribeFromDiscussion', directSubs.discussionId).catch(() => {});
        }
        if (ctx.discussionId && ctx.discussionId !== directSubs.discussionId) {
            connection.invoke('SubscribeToDiscussion', ctx.discussionId).catch(() => {});
        }
        directSubs.discussionId = ctx.discussionId;

        if (directSubs.spaceId && directSubs.spaceId !== ctx.spaceId) {
            connection.invoke('UnsubscribeFromSpace', directSubs.spaceId).catch(() => {});
        }
        if (ctx.spaceId && ctx.spaceId !== directSubs.spaceId) {
            connection.invoke('SubscribeToSpace', ctx.spaceId).catch(() => {});
        }
        directSubs.spaceId = ctx.spaceId;

        if (directSubs.hubId && directSubs.hubId !== ctx.hubId) {
            connection.invoke('UnsubscribeFromHub', directSubs.hubId).catch(() => {});
        }
        if (ctx.hubId && ctx.hubId !== directSubs.hubId) {
            connection.invoke('SubscribeToHub', ctx.hubId).catch(() => {});
        }
        directSubs.hubId = ctx.hubId;
    }

    // ============================================================================
    // Typing indicator API (for discussion-detail.ts)
    // ============================================================================

    (window as any).SnakkRealtime = {
        startTyping(discussionId: string, displayName: string): void {
            if (worker) {
                worker.port.postMessage({ type: 'invoke', method: 'StartTyping', args: [discussionId, displayName] });
            } else if (connection?.state === signalR.HubConnectionState.Connected) {
                connection.invoke('StartTyping', discussionId, displayName).catch(() => {});
            }
        },
        stopTyping(discussionId: string, displayName: string): void {
            if (worker) {
                worker.port.postMessage({ type: 'invoke', method: 'StopTyping', args: [discussionId, displayName] });
            } else if (connection?.state === signalR.HubConnectionState.Connected) {
                connection.invoke('StopTyping', discussionId, displayName).catch(() => {});
            }
        }
    };

    // ============================================================================
    // Bootstrap
    // ============================================================================

    function start(): void {
        if (!trySharedWorker()) {
            debugLog('SharedWorker unavailable — using direct connection');
            startDirectConnection();
        }
    }

    if ('requestIdleCallback' in window) requestIdleCallback(start);
    else setTimeout(start, 200);

    // Re-evaluate subscriptions after HTMX boost navigation
    document.addEventListener('htmx:afterSettle', () => {
        if (worker) updateWorkerSubscriptions();
        else if (connection) updateDirectSubscriptions();
    });
})();
