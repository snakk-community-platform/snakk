(function() {
    'use strict';

    // Get API base URL from global variable (set by Layout)
    const apiBaseUrl = window.snakkApiBaseUrl || 'https://localhost:7291';

    // Initialize SignalR connection to API server
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(`${apiBaseUrl}/realtime`)
        .withAutomaticReconnect({
            nextRetryDelayInMilliseconds: retryContext => {
                // Exponential backoff: 0s, 2s, 10s, 30s, then 30s
                if (retryContext.previousRetryCount === 0) return 0;
                if (retryContext.previousRetryCount === 1) return 2000;
                if (retryContext.previousRetryCount === 2) return 10000;
                return 30000;
            }
        })
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
            indicator.onclick = function() {
                const posts = document.querySelectorAll('[id^="post-"]');
                if (posts.length > 0) {
                    posts[posts.length - 1].scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
                indicator.remove();
            };
            document.body.appendChild(indicator);
        }
    }

    // Handle incoming updates
    connection.on("ReceiveUpdate", function(message) {
        console.log('Received realtime update:', message.eventType);

        // Handle reaction updates specially
        if (message.eventType === 'reaction-updated') {
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
    function subscribeToGroups() {
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
    }

    // Track current subscriptions to avoid duplicate subscriptions
    let currentSubscriptions = {
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
        currentSubscriptions.hubSlug = hubSlug || null;
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

    // Update subscriptions when navigating via HTMX
    document.body.addEventListener('htmx:load', function(evt) {
        // Wait a tick for page scripts to update body data attributes
        setTimeout(updateSubscriptions, 100);
    });

    connection.onreconnecting(() => {
        console.log('⏳ Reconnecting to realtime server...');
    });

    connection.onclose(() => {
        console.log('❌ Realtime connection closed');
    });

    // Handle notification count updates
    connection.on("ReceiveNotificationCount", function(data) {
        console.log('Notification count update:', data.unreadCount);
        if (typeof updateNotificationBadge === 'function') {
            updateNotificationBadge(data.unreadCount);
        }
    });

    // Handle new notifications
    connection.on("ReceiveNotification", function(notification) {
        console.log('New notification:', notification.type);

        // Update badge count
        const badge = document.getElementById('notification-badge');
        if (badge) {
            const currentCount = parseInt(badge.textContent) || 0;
            badge.textContent = currentCount + 1;
            badge.classList.remove('hidden');
        }

        // Optionally show a toast notification
        if (typeof showNotificationToast === 'function') {
            showNotificationToast(notification);
        }

        // Refresh notification list if visible
        if (typeof loadNotifications === 'function') {
            loadNotifications();
        }
    });

    // Handle reaction updates
    function handleReactionUpdate(postId, counts) {
        const reactionsBar = document.getElementById(`reactions-${postId}`);
        if (!reactionsBar) return;

        const reactionEmojis = { ThumbsUp: '👍', Heart: '❤️', Eyes: '👀' };
        let html = '';

        // Add reactions with counts
        for (const [type, emoji] of Object.entries(reactionEmojis)) {
            const count = counts[type] || 0;
            if (count > 0) {
                html += `<button type="button" class="reaction-pill" data-type="${type}" onclick="toggleReaction('${postId}', '${type}')">${emoji} <span class="count">${count}</span></button>`;
            }
        }

        // Add the "+" button
        html += `<button type="button" class="reaction-pill add-reaction" onclick="toggleReactionPicker('${postId}')" title="Add reaction">+</button>`;

        reactionsBar.innerHTML = html;
    }

    // Subscribe to user notifications if logged in
    function subscribeToUserNotifications() {
        const userId = window.currentUserId;
        if (userId) {
            console.log('Subscribing to user notifications:', userId);
            connection.invoke("SubscribeToUserNotifications", userId)
                .catch(err => console.error('Failed to subscribe to user notifications:', err));
        }
    }

    // Expose connection for debugging
    window.snakkRealtime = connection;

    // Also subscribe to user notifications after connection starts
    const originalSubscribe = subscribeToGroups;
    subscribeToGroups = function() {
        originalSubscribe();
        subscribeToUserNotifications();
    };
})();
