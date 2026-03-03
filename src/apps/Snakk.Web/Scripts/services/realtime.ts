/**
 * SignalR Realtime Connection Manager
 * Handles WebSocket connection to Snakk.Realtime service
 *
 * Note: SignalR is loaded globally via script tag in HTML
 * Type definitions are in global.d.ts
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface RealtimeMessage {
    eventType: string;
    targetId: string;
    htmlContent: string;
    swapStrategy: 'beforeend' | 'afterbegin' | 'innerHTML' | 'outerHTML';
    postId?: string;
    counts?: ReactionCounts;
}

interface ReactionCounts {
    ThumbsUp?: number;
    Heart?: number;
    Eyes?: number;
    Crazy?: number;
    [key: string]: number | undefined;
}

interface NotificationData {
    unreadCount: number;
}

interface Notification {
    type: string;
    [key: string]: any;
}

interface Subscriptions {
    discussionId: string | null;
    spaceSlug: string | null;
    hubSlug: string | null;
}


// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    // Sanitize HTML to prevent XSS from SignalR-delivered content
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

    // Use SNAKK debug logger if available, otherwise noop
    const snakkDebug = (window as any).SnakkDebug;
    function debugLog(message: string): void {
        if (snakkDebug?.log) {
            snakkDebug.log('SignalR', message);
        } else {
            console.debug(message);
        }
    }

    // Connection is created lazily after SignalR library loads
    const realtimeUrl = window.realtimeServiceUrl || 'https://localhost:17101/realtime';
    let connection: signalR.HubConnection | null = null;

    // Track if user is near bottom of page for auto-scroll
    function isNearBottom(threshold: number = 200): boolean {
        const scrollTop = window.scrollY || document.documentElement.scrollTop;
        const windowHeight = window.innerHeight;
        const documentHeight = document.documentElement.scrollHeight;
        return (scrollTop + windowHeight) >= (documentHeight - threshold);
    }

    // Show "new post" indicator when user is scrolled up
    function showNewPostIndicator(): void {
        let indicator = document.getElementById('new-post-indicator');
        if (!indicator) {
            indicator = document.createElement('button');
            indicator.id = 'new-post-indicator';
            indicator.className = 'fixed bottom-24 right-6 btn btn-primary btn-sm shadow-lg z-50 animate-bounce';
            indicator.innerHTML = '↓ New post';
            indicator.onclick = function() {
                const posts = document.querySelectorAll<HTMLElement>('[id^="post-"]');
                if (posts.length > 0) {
                    const lastPost = posts[posts.length - 1];
                    if (lastPost) {
                        lastPost.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }
                }
                indicator?.remove();
            };
            document.body.appendChild(indicator);
        }
    }

    // Handle reaction updates — write server counts to data-attrs and re-render
    function handleReactionUpdate(postId: string, counts: ReactionCounts): void {
        const reactionsBar = document.getElementById(`reactions-${postId}`);
        if (!reactionsBar) return;

        const reactionEmojis: Record<string, string> = {
            ThumbsUp: '👍',
            Heart: '❤️',
            Eyes: '👀',
            Crazy: '🤯'
        };
        const dataKeys: Record<string, string> = {
            ThumbsUp: 'thumbsup',
            Heart: 'heart',
            Eyes: 'eyes',
            Crazy: 'crazy'
        };

        // Update data-count-* attributes with server truth
        let html = '';
        for (const [type, emoji] of Object.entries(reactionEmojis)) {
            const count = counts[type] || 0;
            reactionsBar.setAttribute(`data-count-${dataKeys[type]}`, String(count));
            if (count > 0) {
                html += `<span data-type="${type}">${emoji} ${count}</span>`;
            }
        }

        if (!html) {
            html = '<span class="hidden group-hover:inline" data-reaction-placeholder><svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14.828 14.828a4 4 0 01-5.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg></span>';
        }

        reactionsBar.innerHTML = html;
    }

    // Subscribe to groups based on page context
    function subscribeToGroups(): void {
        if (!connection) return;

        // Always subscribe to global
        connection.invoke("SubscribeToGlobal")
            .catch(_err => debugLog('Failed to subscribe to global'));

        // Check current page and subscribe accordingly
        const discussionId = document.body.dataset.discussionId;
        const spaceSlug = document.body.dataset.spaceSlug;
        const hubSlug = document.body.dataset.hubSlug;

        if (discussionId) {
            debugLog(`Subscribe: discussion ${discussionId}`);
            connection.invoke("SubscribeToDiscussion", discussionId)
                .catch(_err => debugLog('Failed to subscribe to discussion'));
        }

        if (spaceSlug && hubSlug) {
            debugLog(`Subscribe: space ${hubSlug}/${spaceSlug}`);
            connection.invoke("SubscribeToSpace", hubSlug, spaceSlug)
                .catch(_err => debugLog('Failed to subscribe to space'));
        }

        if (hubSlug) {
            debugLog(`Subscribe: hub ${hubSlug}`);
            connection.invoke("SubscribeToHub", hubSlug)
                .catch(_err => debugLog('Failed to subscribe to hub'));
        }

        subscribeToUserNotifications();
    }

    // Track current subscriptions to avoid duplicate subscriptions
    const currentSubscriptions: Subscriptions = {
        discussionId: null,
        spaceSlug: null,
        hubSlug: null
    };

    // Update subscriptions based on current page context
    function updateSubscriptions(): void {
        if (!connection) return;

        const discussionId = document.body.dataset.discussionId;
        const spaceSlug = document.body.dataset.spaceSlug;
        const hubSlug = document.body.dataset.hubSlug;

        // Unsubscribe from old discussion if changed
        if (currentSubscriptions.discussionId && currentSubscriptions.discussionId !== discussionId) {
            debugLog(`Unsubscribe: discussion ${currentSubscriptions.discussionId}`);
            connection.invoke("UnsubscribeFromDiscussion", currentSubscriptions.discussionId)
                .catch(_err => debugLog('Failed to unsubscribe from discussion'));
        }

        // Subscribe to new discussion
        if (discussionId && discussionId !== currentSubscriptions.discussionId) {
            debugLog(`Subscribe: discussion ${discussionId}`);
            connection.invoke("SubscribeToDiscussion", discussionId)
                .catch(_err => debugLog('Failed to subscribe to discussion'));
        }
        currentSubscriptions.discussionId = discussionId || null;

        // Unsubscribe from old space if changed
        if (currentSubscriptions.spaceSlug && currentSubscriptions.hubSlug &&
            (currentSubscriptions.spaceSlug !== spaceSlug || currentSubscriptions.hubSlug !== hubSlug)) {
            debugLog(`Unsubscribe: space ${currentSubscriptions.hubSlug}/${currentSubscriptions.spaceSlug}`);
            connection.invoke("UnsubscribeFromSpace", currentSubscriptions.hubSlug, currentSubscriptions.spaceSlug)
                .catch(_err => debugLog('Failed to unsubscribe from space'));
        }

        // Subscribe to new space
        if (spaceSlug && hubSlug && (spaceSlug !== currentSubscriptions.spaceSlug || hubSlug !== currentSubscriptions.hubSlug)) {
            debugLog(`Subscribe: space ${hubSlug}/${spaceSlug}`);
            connection.invoke("SubscribeToSpace", hubSlug, spaceSlug)
                .catch(_err => debugLog('Failed to subscribe to space'));
        }
        currentSubscriptions.spaceSlug = spaceSlug || null;
        currentSubscriptions.hubSlug = hubSlug || null;

        // Unsubscribe from old hub if changed
        if (currentSubscriptions.hubSlug && currentSubscriptions.hubSlug !== hubSlug) {
            debugLog(`Unsubscribe: hub ${currentSubscriptions.hubSlug}`);
            connection.invoke("UnsubscribeFromHub", currentSubscriptions.hubSlug)
                .catch(_err => debugLog('Failed to unsubscribe from hub'));
        }

        // Subscribe to new hub
        if (hubSlug && hubSlug !== currentSubscriptions.hubSlug) {
            debugLog(`Subscribe: hub ${hubSlug}`);
            connection.invoke("SubscribeToHub", hubSlug)
                .catch(_err => debugLog('Failed to subscribe to hub'));
        }
    }

    // Subscribe to user notifications if logged in
    function subscribeToUserNotifications(): void {
        if (!connection) return;

        const userId = window.currentUserId;
        if (userId) {
            debugLog(`Subscribe: notifications ${userId}`);
            connection.invoke("SubscribeToUserNotifications", userId)
                .catch(_err => debugLog('Failed to subscribe to notifications'));
        }
    }

    // Load SignalR library on demand from meta tag URL
    function loadSignalR(): Promise<void> {
        return new Promise((resolve, reject) => {
            if (typeof signalR !== 'undefined') {
                resolve();
                return;
            }

            var meta = document.querySelector('meta[name="signalr-src"]');
            if (!meta) {
                reject(new Error('SignalR meta tag not found'));
                return;
            }

            var script = document.createElement('script');
            script.src = meta.getAttribute('content')!;
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('Failed to load SignalR'));
            document.body.appendChild(script);
        });
    }

    // Start connection after browser is idle to avoid competing with initial render
    function startConnection(): void {
        loadSignalR()
            .then(() => {
                connection = new signalR.HubConnectionBuilder()
                    .withUrl(realtimeUrl)
                    .withAutomaticReconnect([0, 2000, 10000, 30000])
                    .build();

                // Register event handlers
                connection.on("ReceiveUpdate", handleReceiveUpdate);
                connection.on("ReceiveNotificationCount", handleNotificationCount);
                connection.on("ReceiveNotification", handleNotification);

                connection.onreconnected(() => {
                    debugLog('Reconnected');
                    subscribeToGroups();
                });
                connection.onreconnecting(() => debugLog('Reconnecting...'));
                connection.onclose(() => debugLog('Disconnected'));

                window.snakkRealtime = connection;

                return connection.start();
            })
            .then(() => {
                debugLog('Connected');
                subscribeToGroups();
            })
            .catch(_err => {
                debugLog('Connection error');
            });
    }

    // Event handlers (defined as named functions for registration)
    function handleReceiveUpdate(message: RealtimeMessage): void {
        debugLog(`Update: ${message.eventType}`);

        if (message.eventType === 'reaction-updated' && message.postId && message.counts) {
            handleReactionUpdate(message.postId, message.counts);
            return;
        }

        const target = document.getElementById(message.targetId);
        if (!target) {
            debugLog(`Target not found: ${message.targetId}`);
            return;
        }

        const wasNearBottom = isNearBottom();
        const safeHtml = sanitizeHtml(message.htmlContent);

        switch (message.swapStrategy) {
            case "beforeend":
                target.insertAdjacentHTML('beforeend', safeHtml);
                if (message.eventType === 'post-created') {
                    const newElement = target.lastElementChild;
                    if (newElement) {
                        newElement.classList.add('post-new');
                        setTimeout(() => newElement.classList.remove('post-new'), 500);
                        if (wasNearBottom) {
                            newElement.scrollIntoView({ behavior: 'smooth', block: 'end' });
                        } else {
                            showNewPostIndicator();
                        }
                    }
                }
                break;
            case "afterbegin":
                target.insertAdjacentHTML('afterbegin', safeHtml);
                break;
            case "innerHTML":
                target.innerHTML = safeHtml;
                break;
            case "outerHTML":
                if (message.htmlContent === "") {
                    target.remove();
                } else {
                    target.outerHTML = safeHtml;
                }
                break;
            default:
                target.innerHTML = safeHtml;
        }

        if (typeof htmx !== 'undefined') {
            htmx.process(target.parentElement || document.body);
        }
    }

    function handleNotificationCount(data: NotificationData): void {
        debugLog(`Notification count: ${data.unreadCount}`);
        document.dispatchEvent(new CustomEvent('snakk:realtime:notification-count', {
            detail: { unreadCount: data.unreadCount }
        }));
    }

    function handleNotification(notification: Notification): void {
        debugLog(`Notification: ${notification.type}`);
        document.dispatchEvent(new CustomEvent('snakk:realtime:notification', {
            detail: { notification }
        }));
    }

    if ('requestIdleCallback' in window) {
        requestIdleCallback(startConnection);
    } else {
        setTimeout(startConnection, 200);
    }

    // Update subscriptions when navigating via HTMX
    document.body.addEventListener('htmx:load', function() {
        if (!connection) return;
        setTimeout(updateSubscriptions, 100);
    });
})();
