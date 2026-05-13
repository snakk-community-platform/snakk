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
    spaceId?: string;
    hubId?: string;
    title?: string;
    delta?: number;
    count?: number;
    debatePositions?: DebatePosition[];
    pollOptions?: PollOption[];
    totalVotes?: number;
    lastPostExcerpt?: string;
    lastReplierId?: string;
    lastReplierName?: string;
    lastReplierAvatarUrl?: string;
    lastActivityAtUnix?: number;
}

interface DebatePosition {
    index: number;
    label: string;
    postCount: number;
    pct: number;
}

interface PollOption {
    text: string;
    voteCount: number;
    pct: number;
}

interface ReactionCounts {
    ThumbsUp?: number;
    Heart?: number;
    Eyes?: number;
    Crazy?: number;
    [key: string]: number | undefined;
}

interface ViewerInfo {
    userId: string;
    displayName: string;
    isAnon?: boolean;
}

interface ViewerCountData {
    count: number;
    viewers?: ViewerInfo[];
    group?: string;
}

interface TypingData {
    userId: string;
    displayName: string;
    isTyping: boolean;
    isAnon?: boolean;
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

    const realtimeUrl = document.querySelector<HTMLMetaElement>('meta[name="realtime-service-url"]')?.content
        || 'https://localhost:17103/realtime';

    const _dp = (window as unknown as { DOMPurify?: { sanitize: (html: string, cfg?: object) => string } }).DOMPurify;
    function sanitizeHtml(html: string): string {
        if (!html) return '';
        return _dp ? _dp.sanitize(html, { USE_PROFILES: { html: true } }) : '';
    }

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

        const badge = reactionsBar.closest<HTMLElement>('.sn-reaction-badge');
        if (badge && badge.hasAttribute('data-action')) {
            reactionsBar.innerHTML = html;
            const hasAny = !!html;
            const myReactions: string[] = JSON.parse(reactionsBar.dataset.myReactions || '[]');
            badge.querySelectorAll<HTMLElement>(':scope > .icon').forEach(el => el.remove());
            const countsDiv = badge.querySelector<HTMLElement>('[id^="reactions-"]');
            if (countsDiv) {
                const makeIcon = (name: string): HTMLSpanElement => {
                    const span = document.createElement('span');
                    span.className = `icon ${name} h-4 w-4`;
                    span.setAttribute('aria-hidden', 'true');
                    return span;
                };
                if (!hasAny) {
                    badge.insertBefore(makeIcon('icon-plus-circle'), countsDiv);
                    badge.insertBefore(makeIcon('icon-badge-check'), countsDiv);
                } else if (myReactions.length > 0) {
                    badge.insertBefore(makeIcon('icon-refresh'), countsDiv);
                } else {
                    badge.insertBefore(makeIcon('icon-plus-circle'), countsDiv);
                }
            }
        } else {
            if (!html) {
                html = '<span class="hidden group-hover:inline" data-reaction-placeholder><svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14.828 14.828a4 4 0 01-5.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg></span>';
            }
            reactionsBar.innerHTML = html;
        }
    }

    function handleDiscussionLockChange(isLocked: boolean): void {
        document.getElementById('discussion-lock-banner')?.classList.toggle('hidden', !isLocked);
        document.getElementById('reply-form')?.classList.toggle('hidden', isLocked);
    }

    function handleDiscussionPinned(discussionId: string, isPinned: boolean): void {
        const escaped = CSS.escape(discussionId);
        const item = document.querySelector<HTMLElement>(
            `.topic-item-wrapper[data-discussion-id="${escaped}"],
             article.sn-card[data-discussion-id="${escaped}"]`
        );
        if (!item) return;
        item.classList.toggle('is-pinned', isPinned);
        const container = item.parentElement;
        if (!container) return;
        if (isPinned) container.prepend(item);
    }

    function handleDiscussionDeleted(discussionId: string): void {
        const escaped = CSS.escape(discussionId);
        document.querySelectorAll(
            `.topic-item-wrapper[data-discussion-id="${escaped}"],
             article.sn-card[data-discussion-id="${escaped}"]`
        ).forEach(el => el.remove());
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

    function handleDebateUpdated(discussionId: string, positions: DebatePosition[]): void {
        const escaped = CSS.escape(discussionId);
        document.querySelectorAll<HTMLElement>(`article.sn-card[data-discussion-id="${escaped}"]`).forEach(card => {
            const bar = card.querySelector<HTMLElement>('.sn-debate-bar');
            const labelsEl = card.querySelector<HTMLElement>('.sn-debate-labels');
            if (!bar || !labelsEl) return;
            positions.forEach(pos => {
                const seg = bar.querySelector<HTMLElement>(`[data-position-index="${pos.index}"]`);
                if (seg) {
                    seg.dataset.debateWidth = `${pos.pct}%`;
                    seg.style.width = `${pos.pct}%`;
                    const labelSpan = seg.querySelector<HTMLElement>(':scope > span');
                    if (pos.pct >= 15) {
                        if (!labelSpan) {
                            const span = document.createElement('span');
                            span.textContent = pos.label;
                            seg.appendChild(span);
                        }
                    } else {
                        labelSpan?.remove();
                    }
                }
                const labelEl = labelsEl.querySelector<HTMLElement>(`[data-position-index="${pos.index}"]`);
                if (labelEl) {
                    Array.from(labelEl.childNodes)
                        .filter(n => n.nodeType === Node.TEXT_NODE)
                        .forEach(n => (n as ChildNode).remove());
                    labelEl.appendChild(document.createTextNode(` ${pos.label} ${pos.pct}%`));
                }
            });
        });
    }

    function handlePollUpdated(discussionId: string, options: PollOption[]): void {
        const escaped = CSS.escape(discussionId);
        document.querySelectorAll<HTMLElement>(`article.sn-card[data-discussion-id="${escaped}"]`).forEach(card => {
            const pollPreview = card.querySelector<HTMLElement>('.sn-poll-preview');
            if (!pollPreview) return;
            const pollOptions = Array.from(pollPreview.querySelectorAll<HTMLElement>('.sn-poll-option'));
            options.forEach(opt => {
                const optionEl = pollOptions.find(el => {
                    const textEl = el.querySelector<HTMLElement>('.sn-poll-label > span:first-child');
                    return textEl?.textContent?.trim() === opt.text;
                });
                if (!optionEl) return;
                const fill = optionEl.querySelector<HTMLElement>('.sn-poll-fill');
                const stats = optionEl.querySelector<HTMLElement>('.sn-poll-stats');
                if (fill) { fill.dataset.pollWidth = `${opt.pct}%`; fill.style.width = `${opt.pct}%`; }
                if (stats) stats.textContent = `${opt.pct}% (${opt.voteCount})`;
            });
        });
    }

    function handleDiscussionReactionCount(discussionId: string, delta: number): void {
        const escaped = CSS.escape(discussionId);
        document.querySelectorAll<HTMLElement>(`article.sn-card[data-discussion-id="${escaped}"]`).forEach(card => {
            const badge = card.querySelector<HTMLElement>('.sn-card-reactions');
            const countEl = badge?.querySelector<HTMLElement>('[data-stat="reaction-count"]');
            if (!badge || !countEl) return;
            const current = parseInt(countEl.textContent || '0', 10);
            const next = Math.max(0, (isNaN(current) ? 0 : current) + delta);
            countEl.textContent = String(next);
            badge.classList.toggle('hidden', next === 0);
        });
    }

    function handlePostCountUpdated(msg: RealtimeMessage): void {
        const discussionId = msg.discussionId!;
        const delta = msg.delta!;
        const escaped = CSS.escape(discussionId);
        document.querySelectorAll<HTMLElement>(
            `.topic-item-wrapper[data-discussion-id="${escaped}"],
             article.sn-card[data-discussion-id="${escaped}"]`
        ).forEach(item => {
            const countEl = item.querySelector<HTMLElement>('[data-stat="post-count"]');
            if (countEl) {
                const current = parseInt(countEl.textContent || '0', 10);
                if (!isNaN(current)) countEl.textContent = String(current + delta);
            }

            if (msg.lastReplierId && msg.lastReplierName && delta > 0) {
                const stripWrapper = item.querySelector<HTMLElement>('[data-reply-strip]');
                if (!stripWrapper) return;

                const strip = document.createElement('div');
                strip.className = 'sn-card-reply-strip';

                const meta = document.createElement('div');
                meta.className = 'sn-card-reply-meta';

                const icon = document.createElement('span');
                icon.className = 'icon icon-chat-bubble-filled';
                icon.style.cssText = 'width:1rem;height:1rem';
                icon.setAttribute('aria-hidden', 'true');

                const excerptSpan = document.createElement('span');
                excerptSpan.className = 'sn-card-reply-excerpt';
                const rawExcerpt = msg.lastPostExcerpt ?? '';
                excerptSpan.textContent = `"${rawExcerpt.length > 100 ? rawExcerpt.slice(0, 100) + '…' : rawExcerpt}"`;

                const dot = document.createElement('span');
                dot.className = 'sn-card-dot';
                dot.textContent = '·';

                const time = document.createElement('time');
                time.className = 'sn-card-reply-time';
                const formatTime = (window as any).SnakkUtils?.formatRelativeTime;
                time.textContent = formatTime && msg.lastActivityAtUnix
                    ? formatTime(new Date(msg.lastActivityAtUnix * 1000))
                    : '';

                const avatar = document.createElement('img');
                avatar.src = msg.lastReplierAvatarUrl ?? '';
                avatar.alt = '';
                avatar.className = 'sn-card-reply-avatar';
                avatar.width = 16;
                avatar.height = 16;
                avatar.loading = 'lazy';

                const userLink = document.createElement('a');
                userLink.href = `/u/${msg.lastReplierId}`;
                userLink.className = 'sn-card-reply-username';
                userLink.dataset.popupType = 'user';
                userLink.dataset.popupId = msg.lastReplierId;
                userLink.dataset.popupName = msg.lastReplierName;
                userLink.textContent = msg.lastReplierName;

                meta.append(icon, excerptSpan, dot, time, avatar, userLink);
                strip.appendChild(meta);
                stripWrapper.replaceChildren(strip);
            }
        });
    }

    function handleReadStateUpdated(discussionId: string): void {
        document.dispatchEvent(new CustomEvent('snakk:realtime:read-state-updated', {
            detail: { discussionId }
        }));
    }

    function handleAnnouncementUpdated(): void {
        document.dispatchEvent(new CustomEvent('snakk:realtime:announcement-updated'));
    }

    function handleDiscussionCreated(discussionId: string, msgSpaceId?: string, msgHubId?: string): void {
        // Deduplicate — same event may arrive from multiple subscribed groups
        if (seenDiscussionIds.has(discussionId)) return;
        seenDiscussionIds.add(discussionId);

        // Filter by page scope when we're on a scoped page
        const ctx = getPageContext();
        if (ctx.spaceId && ctx.spaceId !== msgSpaceId) return;
        if (!ctx.spaceId && ctx.hubId && ctx.hubId !== msgHubId) return;

        const container = document.getElementById('discussions-container');
        if (!container) return;

        newDiscussionCount++;

        let indicator = document.getElementById('new-discussions-indicator') as HTMLButtonElement | null;
        if (!indicator) {
            indicator = document.createElement('button');
            indicator.id = 'new-discussions-indicator';
            indicator.type = 'button';
            indicator.className = 'sn-new-discussions-indicator';
            indicator.addEventListener('click', () => window.location.reload());
            container.insertAdjacentElement('beforebegin', indicator);
        }

        const label = newDiscussionCount === 1 ? '1 new discussion' : `${newDiscussionCount} new discussions`;
        indicator.innerHTML =
            `<span class="sn-ndi-icon" aria-hidden="true">↓</span>` +
            `<span class="sn-ndi-label">${label} — click to refresh</span>`;
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

    function viewerHue(userId: string): number {
        let h = 0;
        for (let i = 0; i < userId.length; i++) h = (Math.imul(31, h) + userId.charCodeAt(i)) | 0;
        return Math.abs(h) % 360;
    }

    function handleViewerCount(data: ViewerCountData): void {
        const el = document.getElementById('viewer-presence');
        if (!el) return;
        const myId = document.querySelector<HTMLMetaElement>('meta[name="current-user-id"]')?.content ?? '';
        const others = (data.viewers ?? []).filter(v => v.userId !== myId);
        renderPresenceBubbles(el, others);
    }

    const seenDiscussionIds = new Set<string>();
    let newDiscussionCount = 0;

    function shuffle<T>(arr: T[]): T[] {
        for (let i = arr.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [arr[i], arr[j]] = [arr[j]!, arr[i]!];
        }
        return arr;
    }

    function sortPresence(users: ViewerInfo[]): ViewerInfo[] {
        const named = shuffle(users.filter(v => !v.isAnon));
        const anon  = shuffle(users.filter(v =>  v.isAnon));
        return [...named, ...anon];
    }

    function renderPresenceBubbles(el: HTMLElement, users: ViewerInfo[]): void {
        if (users.length === 0) {
            el.classList.add('hidden');
            el.innerHTML = '';
            return;
        }

        const MAX_SHOW = 5;
        const sorted   = sortPresence(users);
        const shown    = sorted.slice(0, MAX_SHOW);
        const overflow = sorted.length - MAX_SHOW;

        el.innerHTML = '';
        shown.forEach(v => {
            const wrap = document.createElement('div');
            wrap.className = 'tooltip tooltip-bottom';
            wrap.setAttribute('data-tip', v.isAnon ? '?' : v.displayName);

            const bubble = document.createElement('div');
            bubble.className = 'w-6 h-6 rounded-full flex items-center justify-center text-xs font-semibold leading-none select-none cursor-default';

            if (v.isAnon) {
                bubble.classList.add('bg-base-300', 'text-base-content/50');
                const icon = document.createElement('span');
                icon.className = 'icon icon-user';
                icon.style.cssText = 'width:0.875rem;height:0.875rem';
                icon.setAttribute('aria-hidden', 'true');
                bubble.appendChild(icon);
            } else {
                bubble.classList.add('text-white');
                bubble.style.background = `oklch(0.55 0.15 ${viewerHue(v.userId)})`;
                bubble.textContent = (v.displayName.trim().charAt(0) || '?').toUpperCase();
            }

            wrap.appendChild(bubble);
            el.appendChild(wrap);
        });

        if (overflow > 0) {
            const more = document.createElement('div');
            more.className = 'w-6 h-6 rounded-full flex items-center justify-center text-xs font-medium leading-none select-none cursor-default bg-base-300 text-base-content/70';
            more.textContent = `+${overflow}`;
            el.appendChild(more);
        }

        el.classList.remove('hidden');
    }

    // Fallback timeout: 5 min safety net for missed StopTyping (crash/network drop).
    // Normal stop comes via StopTyping hub call or 3-min client inactivity timeout.
    const TYPING_FALLBACK_MS = 5 * 60 * 1000;
    const typingUsers = new Map<string, { displayName: string; isAnon: boolean; timeout: ReturnType<typeof setTimeout> }>();

    function handleTypingIndicator(data: TypingData): void {
        if (!data.userId) return;
        if (data.isTyping) {
            const existing = typingUsers.get(data.userId);
            if (existing) clearTimeout(existing.timeout);
            typingUsers.set(data.userId, {
                displayName: data.displayName,
                isAnon: data.isAnon ?? false,
                timeout: setTimeout(() => {
                    typingUsers.delete(data.userId);
                    renderTypingIndicator();
                }, TYPING_FALLBACK_MS)
            });
        } else {
            const existing = typingUsers.get(data.userId);
            if (existing) clearTimeout(existing.timeout);
            typingUsers.delete(data.userId);
        }
        renderTypingIndicator();
    }

    function renderTypingIndicator(): void {
        const el = document.getElementById('typing-indicator');
        if (!el) return;
        const users: ViewerInfo[] = Array.from(typingUsers.entries())
            .map(([userId, v]) => ({ userId, displayName: v.displayName, isAnon: v.isAnon }));
        renderPresenceBubbles(el, users);
    }

    function handleReceiveUpdate(message: RealtimeMessage): void {
        debugLog(`Update: ${message.eventType}`);

        if (message.eventType === 'reaction-updated' && message.postId && message.counts) {
            handleReactionUpdate(message.postId, message.counts);
            return;
        }
        if (message.eventType === 'discussion-locked') { handleDiscussionLockChange(true); return; }
        if (message.eventType === 'discussion-unlocked') { handleDiscussionLockChange(false); return; }
        if (message.eventType === 'discussion-created' && message.discussionId) {
            handleDiscussionCreated(message.discussionId, message.spaceId, message.hubId);
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
            handlePostCountUpdated(message);
            return;
        }
        if (message.eventType === 'discussion-reaction-count-updated' && message.discussionId && message.delta != null) {
            handleDiscussionReactionCount(message.discussionId, message.delta);
            return;
        }
        if (message.eventType === 'debate-updated' && message.discussionId && message.debatePositions) {
            handleDebateUpdated(message.discussionId, message.debatePositions);
            return;
        }
        if (message.eventType === 'poll-updated' && message.discussionId && message.pollOptions) {
            handlePollUpdated(message.discussionId, message.pollOptions);
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
        startTyping(discussionId: string): void {
            if (worker) {
                worker.port.postMessage({ type: 'invoke', method: 'StartTyping', args: [discussionId] });
            } else if (connection?.state === signalR.HubConnectionState.Connected) {
                connection.invoke('StartTyping', discussionId).catch(() => {});
            }
        },
        stopTyping(discussionId: string): void {
            if (worker) {
                worker.port.postMessage({ type: 'invoke', method: 'StopTyping', args: [discussionId] });
            } else if (connection?.state === signalR.HubConnectionState.Connected) {
                connection.invoke('StopTyping', discussionId).catch(() => {});
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
