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

    // Initialize SignalR connection to dedicated Realtime service
    const realtimeUrl = window.realtimeServiceUrl || 'http://localhost:5300/realtime';
    // Exponential backoff: 0s, 2s, 10s, 30s (SignalR will repeat the last value)
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(realtimeUrl)
        .withAutomaticReconnect([0, 2000, 10000, 30000])
        .build();

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

    // Handle incoming updates
    connection.on("ReceiveUpdate", function(message: RealtimeMessage): void {
        console.log('Received realtime update:', message.eventType);

        // Handle reaction updates specially
        if (message.eventType === 'reaction-updated' && message.postId && message.counts) {
            handleReactionUpdate(message.postId, message.counts);
            return;
        }

        const target = document.getElementById(message.targetId);
        if (!target) {
            console.warn('Target element not found:', message.targetId);
            return;
        }

        const wasNearBottom = isNearBottom();

        // HTMX-compatible DOM updates
        switch (message.swapStrategy) {
            case "beforeend":
                target.insertAdjacentHTML('beforeend', message.htmlContent);
                // Add animation class to new posts
                if (message.eventType === 'post-created') {
                    const newElement = target.lastElementChild;
                    if (newElement) {
                        newElement.classList.add('post-new');
                        // Remove animation class after animation completes
                        setTimeout(() => newElement.classList.remove('post-new'), 500);

                        // Auto-scroll if user was near bottom, otherwise show indicator
                        if (wasNearBottom) {
                            newElement.scrollIntoView({ behavior: 'smooth', block: 'end' });
                        } else {
                            showNewPostIndicator();
                        }
                    }
                }
                break;
            case "afterbegin":
                target.insertAdjacentHTML('afterbegin', message.htmlContent);
                break;
            case "innerHTML":
                target.innerHTML = message.htmlContent;
                break;
            case "outerHTML":
                if (message.htmlContent === "") {
                    // Hard delete - remove element
                    target.remove();
                } else {
                    target.outerHTML = message.htmlContent;
                }
                break;
            default:
                target.innerHTML = message.htmlContent;
        }

        // Trigger HTMX processing for new content (if needed)
        if (typeof htmx !== 'undefined') {
            htmx.process(target.parentElement || document.body);
        }
    });

    // Subscribe to groups based on page context
    function subscribeToGroups(): void {
        // Always subscribe to global
        connection.invoke("SubscribeToGlobal")
            .catch(err => console.error('Failed to subscribe to global:', err));

        // Check current page and subscribe accordingly
        const discussionId = document.body.dataset.discussionId;
        const spaceSlug = document.body.dataset.spaceSlug;
        const hubSlug = document.body.dataset.hubSlug;

        if (discussionId) {
            console.log('Subscribing to discussion:', discussionId);
            connection.invoke("SubscribeToDiscussion", discussionId)
                .catch(err => console.error('Failed to subscribe to discussion:', err));
        }

        if (spaceSlug && hubSlug) {
            console.log('Subscribing to space:', hubSlug, spaceSlug);
            connection.invoke("SubscribeToSpace", hubSlug, spaceSlug)
                .catch(err => console.error('Failed to subscribe to space:', err));
        }

        if (hubSlug) {
            console.log('Subscribing to hub:', hubSlug);
            connection.invoke("SubscribeToHub", hubSlug)
                .catch(err => console.error('Failed to subscribe to hub:', err));
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
        const discussionId = document.body.dataset.discussionId;
        const spaceSlug = document.body.dataset.spaceSlug;
        const hubSlug = document.body.dataset.hubSlug;

        // Unsubscribe from old discussion if changed
        if (currentSubscriptions.discussionId && currentSubscriptions.discussionId !== discussionId) {
            console.log('Unsubscribing from old discussion:', currentSubscriptions.discussionId);
            connection.invoke("UnsubscribeFromDiscussion", currentSubscriptions.discussionId)
                .catch(err => console.warn('Failed to unsubscribe from discussion:', err));
        }

        // Subscribe to new discussion
        if (discussionId && discussionId !== currentSubscriptions.discussionId) {
            console.log('Subscribing to discussion:', discussionId);
            connection.invoke("SubscribeToDiscussion", discussionId)
                .catch(err => console.error('Failed to subscribe to discussion:', err));
        }
        currentSubscriptions.discussionId = discussionId || null;

        // Unsubscribe from old space if changed
        if (currentSubscriptions.spaceSlug && currentSubscriptions.hubSlug &&
            (currentSubscriptions.spaceSlug !== spaceSlug || currentSubscriptions.hubSlug !== hubSlug)) {
            console.log('Unsubscribing from old space:', currentSubscriptions.hubSlug, currentSubscriptions.spaceSlug);
            connection.invoke("UnsubscribeFromSpace", currentSubscriptions.hubSlug, currentSubscriptions.spaceSlug)
                .catch(err => console.warn('Failed to unsubscribe from space:', err));
        }

        // Subscribe to new space
        if (spaceSlug && hubSlug && (spaceSlug !== currentSubscriptions.spaceSlug || hubSlug !== currentSubscriptions.hubSlug)) {
            console.log('Subscribing to space:', hubSlug, spaceSlug);
            connection.invoke("SubscribeToSpace", hubSlug, spaceSlug)
                .catch(err => console.error('Failed to subscribe to space:', err));
        }
        currentSubscriptions.spaceSlug = spaceSlug || null;
        currentSubscriptions.hubSlug = hubSlug || null;

        // Unsubscribe from old hub if changed
        if (currentSubscriptions.hubSlug && currentSubscriptions.hubSlug !== hubSlug) {
            console.log('Unsubscribing from old hub:', currentSubscriptions.hubSlug);
            connection.invoke("UnsubscribeFromHub", currentSubscriptions.hubSlug)
                .catch(err => console.warn('Failed to unsubscribe from hub:', err));
        }

        // Subscribe to new hub
        if (hubSlug && hubSlug !== currentSubscriptions.hubSlug) {
            console.log('Subscribing to hub:', hubSlug);
            connection.invoke("SubscribeToHub", hubSlug)
                .catch(err => console.error('Failed to subscribe to hub:', err));
        }
    }

    // Subscribe to user notifications if logged in
    function subscribeToUserNotifications(): void {
        const userId = window.currentUserId;
        if (userId) {
            console.log('Subscribing to user notifications:', userId);
            connection.invoke("SubscribeToUserNotifications", userId)
                .catch(err => console.error('Failed to subscribe to user notifications:', err));
        }
    }

    // Start connection
    connection.start()
        .then(() => {
            console.log('✅ Realtime connection established');
            subscribeToGroups();
        })
        .catch(err => {
            console.error('❌ SignalR connection error:', err);
        });

    // Re-subscribe on reconnect (idempotent)
    connection.onreconnected(() => {
        console.log('🔄 Reconnected to realtime server');
        subscribeToGroups();
    });

    connection.onreconnecting(() => {
        console.log('⏳ Reconnecting to realtime server...');
    });

    connection.onclose(() => {
        console.log('❌ Realtime connection closed');
    });

    // Update subscriptions when navigating via HTMX
    document.body.addEventListener('htmx:load', function() {
        // Wait a tick for page scripts to update body data attributes
        setTimeout(updateSubscriptions, 100);
    });

    // Handle notification count updates
    connection.on("ReceiveNotificationCount", function(data: NotificationData): void {
        console.log('Notification count update:', data.unreadCount);

        // Dispatch custom event
        document.dispatchEvent(new CustomEvent('snakk:realtime:notification-count', {
            detail: { unreadCount: data.unreadCount }
        }));
    });

    // Handle new notifications
    connection.on("ReceiveNotification", function(notification: Notification): void {
        console.log('New notification:', notification.type);

        // Dispatch custom event
        document.dispatchEvent(new CustomEvent('snakk:realtime:notification', {
            detail: { notification }
        }));
    });

    // Expose connection for debugging
    window.snakkRealtime = connection;
})();
