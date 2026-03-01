"use strict";
/**
 * SignalR Realtime Connection Manager
 * Handles WebSocket connection to Snakk.Realtime service
 *
 * Note: SignalR is loaded globally via script tag in HTML
 * Type definitions are in global.d.ts
 */
// ============================================================================
// Implementation
// ============================================================================
(function () {
    'use strict';
    // Use SNAKK debug logger if available, otherwise noop
    const snakkDebug = window.SnakkDebug;
    function debugLog(message) {
        if (snakkDebug?.log) {
            snakkDebug.log('SignalR', message);
        }
        else {
            console.debug(message);
        }
    }
    // Initialize SignalR connection to dedicated Realtime service
    const realtimeUrl = window.realtimeServiceUrl || 'http://localhost:5300/realtime';
    // Exponential backoff: 0s, 2s, 10s, 30s (SignalR will repeat the last value)
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(realtimeUrl)
        .withAutomaticReconnect([0, 2000, 10000, 30000])
        .build();
    // Track if user is near bottom of page for auto-scroll
    function isNearBottom(threshold = 200) {
        const scrollTop = window.scrollY || document.documentElement.scrollTop;
        const windowHeight = window.innerHeight;
        const documentHeight = document.documentElement.scrollHeight;
        return (scrollTop + windowHeight) >= (documentHeight - threshold);
    }
    // Show "new post" indicator when user is scrolled up
    function showNewPostIndicator() {
        let indicator = document.getElementById('new-post-indicator');
        if (!indicator) {
            indicator = document.createElement('button');
            indicator.id = 'new-post-indicator';
            indicator.className = 'fixed bottom-24 right-6 btn btn-primary btn-sm shadow-lg z-50 animate-bounce';
            indicator.innerHTML = '↓ New post';
            indicator.onclick = function () {
                const posts = document.querySelectorAll('[id^="post-"]');
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
    function handleReactionUpdate(postId, counts) {
        const reactionsBar = document.getElementById(`reactions-${postId}`);
        if (!reactionsBar)
            return;
        const reactionEmojis = {
            ThumbsUp: '👍',
            Heart: '❤️',
            Eyes: '👀',
            Crazy: '🤯'
        };
        const dataKeys = {
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
    connection.on("ReceiveUpdate", function (message) {
        debugLog(`Update: ${message.eventType}`);
        // Handle reaction updates specially
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
                        }
                        else {
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
                }
                else {
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
    function subscribeToGroups() {
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
    const currentSubscriptions = {
        discussionId: null,
        spaceSlug: null,
        hubSlug: null
    };
    // Update subscriptions based on current page context
    function updateSubscriptions() {
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
    function subscribeToUserNotifications() {
        const userId = window.currentUserId;
        if (userId) {
            debugLog(`Subscribe: notifications ${userId}`);
            connection.invoke("SubscribeToUserNotifications", userId)
                .catch(_err => debugLog('Failed to subscribe to notifications'));
        }
    }
    // Start connection
    connection.start()
        .then(() => {
        debugLog('Connected');
        subscribeToGroups();
    })
        .catch(_err => {
        debugLog('Connection error');
    });
    // Re-subscribe on reconnect (idempotent)
    connection.onreconnected(() => {
        debugLog('Reconnected');
        subscribeToGroups();
    });
    connection.onreconnecting(() => {
        debugLog('Reconnecting...');
    });
    connection.onclose(() => {
        debugLog('Disconnected');
    });
    // Update subscriptions when navigating via HTMX
    document.body.addEventListener('htmx:load', function () {
        // Wait a tick for page scripts to update body data attributes
        setTimeout(updateSubscriptions, 100);
    });
    // Handle notification count updates
    connection.on("ReceiveNotificationCount", function (data) {
        debugLog(`Notification count: ${data.unreadCount}`);
        // Dispatch custom event
        document.dispatchEvent(new CustomEvent('snakk:realtime:notification-count', {
            detail: { unreadCount: data.unreadCount }
        }));
    });
    // Handle new notifications
    connection.on("ReceiveNotification", function (notification) {
        debugLog(`Notification: ${notification.type}`);
        // Dispatch custom event
        document.dispatchEvent(new CustomEvent('snakk:realtime:notification', {
            detail: { notification }
        }));
    });
    // Expose connection for debugging
    window.snakkRealtime = connection;
})();
//# sourceMappingURL=realtime.js.map