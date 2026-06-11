/**
 * DM Widget — Facebook-style inline floating chat windows.
 *
 * Appended to document.body outside #main-content, so it survives
 * HTMX boost navigation automatically.
 *
 * Active chat = a conversation the user has explicitly opened.
 * State is kept in sessionStorage so windows survive HTMX navigation
 * within a tab but clear on tab close.
 */

(function (): void {
    'use strict';

    if ((window as any).DmWidget) return;

    // ── Constants ────────────────────────────────────────────────────────────

    const MAX_WINDOWS = 3;
    const MSG_PAGE_SIZE = 30;
    const SESSION_KEY = 'snakk:dm:open';

    // ── Types ────────────────────────────────────────────────────────────────

    interface ConvInfo {
        publicId: string;       // conversation ID
        otherUserPublicId: string;
        displayName: string;
        avatarUrl: string;
        lastMessageExcerpt?: string;
    }

    interface MessageInfo {
        publicId: string;
        content: string;
        createdAt: string;
        isMine: boolean;
        senderPublicId: string;
    }

    interface WindowState {
        conv: ConvInfo;
        el: HTMLElement;
        lastMessageId: string;
    }

    // ── State ────────────────────────────────────────────────────────────────

    let panelOpen = false;
    let panelLoaded = false;
    let isMuted = localStorage.getItem('snakk:dm:muted') === 'true';
    const openWindows = new Map<string, WindowState>();

    // ── DOM refs ─────────────────────────────────────────────────────────────

    let widgetEl!: HTMLElement;
    let panelEl!: HTMLElement;
    let panelListEl!: HTMLElement;
    let toggleBtn!: HTMLButtonElement;

    // ── Helpers ──────────────────────────────────────────────────────────────

    const escapeHtml = (text: string): string => (window as any).SnakkUtils.escapeHtml(text);

    function formatTime(iso: string): string {
        try { return new Date(iso).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' }); }
        catch { return ''; }
    }

    function formatDate(iso: string): string {
        try {
            const date = new Date(iso);
            const now = new Date();
            const sameDay = (a: Date, b: Date) =>
                a.getFullYear() === b.getFullYear() &&
                a.getMonth() === b.getMonth() &&
                a.getDate() === b.getDate();

            if (sameDay(date, now)) return 'Today';

            const yesterday = new Date(now);
            yesterday.setDate(now.getDate() - 1);
            if (sameDay(date, yesterday)) return 'Yesterday';

            const daysAgo = Math.floor((now.getTime() - date.getTime()) / 86_400_000);
            if (daysAgo < 7) return date.toLocaleDateString([], { weekday: 'long' });

            const opts: Intl.DateTimeFormatOptions = { month: 'long', day: 'numeric' };
            if (date.getFullYear() !== now.getFullYear()) opts.year = 'numeric';
            return date.toLocaleDateString([], opts);
        } catch { return ''; }
    }

    function getLastBubbleInfo(messagesEl: HTMLElement): { senderPublicId: string; dateStr: string } | null {
        const bubbles = messagesEl.querySelectorAll<HTMLElement>('.dm-bubble');
        const last = bubbles[bubbles.length - 1];
        if (!last) return null;
        return {
            senderPublicId: last.dataset.senderPublicId ?? '',
            dateStr: last.dataset.dateStr ?? '',
        };
    }

    async function markAsReadAndUpdateBadge(convId: string): Promise<void> {
        try {
            await fetch(`/bff/messages/conversations/${encodeURIComponent(convId)}/read`, {
                method: 'POST', credentials: 'include',
            });
            const res = await fetch('/bff/messages/unread-count', { credentials: 'include' });
            if (res.ok) {
                const data = await res.json() as { count: number };
                document.dispatchEvent(new CustomEvent('snakk:realtime:dm-count', {
                    detail: { unreadCount: data.count }
                }));
            }
        } catch { /* silent */ }
    }

    function nudgeToggleBtn(): void {
        toggleBtn.classList.remove('dm-toggle-btn--nudge');
        void toggleBtn.offsetWidth; // force reflow so animation restarts if already running
        toggleBtn.classList.add('dm-toggle-btn--nudge');
        toggleBtn.addEventListener('animationend', () => {
            toggleBtn.classList.remove('dm-toggle-btn--nudge');
        }, { once: true });
    }

    function playMessageSound(): void {
        if (isMuted) return;
        try {
            const ctx = new AudioContext();
            [0.02, 0.06].forEach((vol, i) => {
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.connect(gain); gain.connect(ctx.destination);
                osc.frequency.value = 555;
                gain.gain.setValueAtTime(vol, ctx.currentTime + i * 0.06);
                gain.gain.linearRampToValueAtTime(0.001, ctx.currentTime + i * 0.06 + 0.02);
                osc.start(ctx.currentTime + i * 0.06);
                osc.stop(ctx.currentTime + i * 0.06 + 0.02);
            });
        } catch { /* AudioContext unavailable */ }
    }

    function renderAvatar(conv: ConvInfo, size: 'sm' | 'xs' = 'sm'): string {
        const initial = escapeHtml((conv.displayName[0] ?? '?').toUpperCase());
        return `<img src="${escapeHtml(conv.avatarUrl)}" alt="" class="dm-avatar dm-avatar--${size}" loading="lazy"
                     onerror="this.style.display='none';this.nextElementSibling.style.removeProperty('display')">
                <span class="dm-avatar-initials dm-avatar--${size}" style="display:none">${initial}</span>`;
    }

    // ── Session persistence ───────────────────────────────────────────────────

    function saveOpenState(): void {
        const state = Array.from(openWindows.values()).map(w => w.conv);
        try { sessionStorage.setItem(SESSION_KEY, JSON.stringify(state)); }
        catch { /* quota */ }
    }

    function loadOpenState(): ConvInfo[] {
        try {
            const raw = sessionStorage.getItem(SESSION_KEY);
            if (!raw) return [];
            const parsed = JSON.parse(raw) as Array<Record<string, unknown>>;
            return parsed
                .filter(c => typeof c.publicId === 'string' && typeof c.displayName === 'string')
                .map(c => ({
                    publicId: c['publicId'] as string,
                    otherUserPublicId: (c['otherUserPublicId'] as string) ?? '',
                    displayName: c['displayName'] as string,
                    avatarUrl: (c['avatarUrl'] as string) ?? '',
                    lastMessageExcerpt: c['lastMessageExcerpt'] as string | undefined,
                }));
        } catch { return []; }
    }

    // ── Widget DOM creation ───────────────────────────────────────────────────

    function createWidget(): void {
        widgetEl = document.createElement('div');
        widgetEl.id = 'dm-widget';

        // Conversation panel (shows recent conversations)
        panelEl = document.createElement('div');
        panelEl.className = 'dm-panel';
        panelEl.style.display = 'none';
        panelEl.innerHTML = `
            <div class="dm-panel-header">
                <span class="font-semibold text-sm">Messages</span>
                <div class="flex items-center gap-2">
                    <button type="button" class="dm-mute-btn" aria-label="Toggle sound">
                        <span class="icon dm-mute-icon" aria-hidden="true"></span>
                    </button>
                    <a href="/messages" class="text-xs dm-panel-view-all" hx-boost="false">View all</a>
                </div>
            </div>
            <div class="dm-panel-list"></div>
        `;
        panelListEl = panelEl.querySelector('.dm-panel-list')!;

        const muteBtn = panelEl.querySelector<HTMLButtonElement>('.dm-mute-btn')!;
        const muteIcon = panelEl.querySelector<HTMLElement>('.dm-mute-icon')!;

        function updateMuteIcon(): void {
            muteIcon.className = `icon ${isMuted ? 'icon-volume-off' : 'icon-volume-up'} dm-mute-icon`;
            muteBtn.title = isMuted ? 'Sound off (click to enable)' : 'Sound on (click to mute)';
        }
        updateMuteIcon();

        muteBtn.addEventListener('click', e => {
            e.stopPropagation();
            isMuted = !isMuted;
            localStorage.setItem('snakk:dm:muted', String(isMuted));
            updateMuteIcon();
        });

        // Toggle button
        toggleBtn = document.createElement('button');
        toggleBtn.className = 'dm-toggle-btn';
        toggleBtn.type = 'button';
        toggleBtn.setAttribute('aria-label', 'Messages');
        toggleBtn.innerHTML = `
            <span class="icon icon-chat-bubbles dm-toggle-icon" aria-hidden="true"></span>
            <span class="dm-badge hidden">0</span>
        `;

        widgetEl.appendChild(panelEl);
        widgetEl.appendChild(toggleBtn);
        document.body.appendChild(widgetEl);

        // Events
        toggleBtn.addEventListener('click', togglePanel);

        // Close panel when clicking outside
        document.addEventListener('click', e => {
            if (panelOpen && !widgetEl.contains(e.target as Node)) {
                closePanel();
            }
        });

        // Intercept /messages/start?recipient=... links to open widget instead
        document.addEventListener('click', e => {
            const link = (e.target as Element).closest<HTMLAnchorElement>('a[href*="/messages/start"]');
            if (!link) return;
            const recipient = new URL(link.href, location.href).searchParams.get('recipient');
            if (!recipient) return;
            e.preventDefault();
            void openWindowForRecipient(recipient);
        });
    }

    // ── Panel ─────────────────────────────────────────────────────────────────

    function togglePanel(): void {
        if (panelOpen) { closePanel(); return; }
        panelOpen = true;
        panelEl.style.display = '';
        toggleBtn.classList.add('dm-toggle-btn--active');
        if (!panelLoaded) { void loadConversationList(); }
    }

    function closePanel(): void {
        panelOpen = false;
        panelEl.style.display = 'none';
        toggleBtn.classList.remove('dm-toggle-btn--active');
    }

    async function loadConversationList(): Promise<void> {
        panelLoaded = true;
        panelListEl.innerHTML = '<div class="dm-panel-loading">Loading…</div>';

        try {
            const res = await fetch('/bff/messages/conversations?offset=0&pageSize=10', { credentials: 'include' });
            if (!res.ok) { panelListEl.innerHTML = '<div class="dm-panel-empty">Could not load</div>'; return; }

            const data = await res.json() as { items: Array<{ publicId: string; otherUser: { publicId: string; displayName: string; avatarUrl: string }; lastMessageExcerpt?: string }> };

            if (!data.items?.length) {
                panelListEl.innerHTML = '<div class="dm-panel-empty">No conversations yet</div>';
                return;
            }

            panelListEl.innerHTML = '';
            for (const c of data.items) {
                const conv: ConvInfo = {
                    publicId: c.publicId,
                    otherUserPublicId: c.otherUser.publicId,
                    displayName: c.otherUser.displayName,
                    avatarUrl: c.otherUser.avatarUrl ?? '',
                    lastMessageExcerpt: c.lastMessageExcerpt,
                };
                const item = document.createElement('button');
                item.type = 'button';
                item.className = 'dm-panel-item';
                item.innerHTML = `
                    ${renderAvatar(conv, 'sm')}
                    <div class="dm-panel-item-text">
                        <div class="dm-panel-item-name">${escapeHtml(conv.displayName)}</div>
                        ${conv.lastMessageExcerpt ? `<div class="dm-panel-item-excerpt">${escapeHtml(conv.lastMessageExcerpt)}</div>` : ''}
                    </div>
                `;
                item.addEventListener('click', () => { openWindow(conv); closePanel(); });
                panelListEl.appendChild(item);
            }
        } catch {
            panelListEl.innerHTML = '<div class="dm-panel-empty">Could not load</div>';
        }
    }

    // ── Chat windows ──────────────────────────────────────────────────────────

    async function openWindowForRecipient(recipientPublicId: string): Promise<void> {
        // Check if already open by matching the other user's publicId
        for (const [, state] of openWindows) {
            if (state.conv.otherUserPublicId === recipientPublicId) {
                focusWindow(state.el);
                return;
            }
        }

        // Create/get conversation via BFF
        try {
            const res = await fetch('/bff/messages/conversations', {
                method: 'POST',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ recipientPublicId }),
            });
            if (!res.ok) return;
            const data = await res.json() as { publicId: string; otherUser: { publicId: string; displayName: string; avatarUrl: string } };
            openWindow({
                publicId: data.publicId,
                otherUserPublicId: data.otherUser.publicId,
                displayName: data.otherUser.displayName,
                avatarUrl: data.otherUser.avatarUrl ?? '',
            });
        } catch { /* silent */ }
    }

    function openWindow(conv: ConvInfo): void {
        if (openWindows.has(conv.publicId)) {
            focusWindow(openWindows.get(conv.publicId)!.el);
            return;
        }

        // Enforce max windows — close oldest
        if (openWindows.size >= MAX_WINDOWS) {
            const oldest = openWindows.keys().next().value;
            if (oldest) closeWindow(oldest);
        }

        const win = createWindowEl(conv);
        // Insert before the panel+toggle group (at start of widgetEl)
        widgetEl.insertBefore(win, panelEl);

        openWindows.set(conv.publicId, { conv, el: win, lastMessageId: '' });
        saveOpenState();

        void loadMessages(conv.publicId);
    }

    function closeWindow(id: string): void {
        const state = openWindows.get(id);
        if (!state) return;
        (window as any).SnakkRealtime?.stopDmTyping?.(id, state.conv.otherUserPublicId);
        state.el.remove();
        openWindows.delete(id);
        saveOpenState();
    }

    function focusWindow(el: HTMLElement): void {
        el.querySelector<HTMLTextAreaElement>('.dm-win-textarea')?.focus();
    }

    function createWindowEl(conv: ConvInfo): HTMLElement {
        const win = document.createElement('div');
        win.className = 'dm-window';
        win.dataset.convId = conv.publicId;
        win.innerHTML = `
            <div class="dm-win-header">
                ${renderAvatar(conv, 'xs')}
                <span class="dm-win-name">${escapeHtml(conv.displayName)}</span>
                <a href="/messages/${encodeURIComponent(conv.publicId)}" class="dm-win-expand" aria-label="Open full conversation" hx-boost="false" title="Open full view">
                    <span class="icon icon-expand-fullscreen" style="width:.875rem;height:.875rem" aria-hidden="true"></span>
                </a>
                <button type="button" class="dm-win-close" aria-label="Close chat">
                    <span class="icon icon-x" style="width:.875rem;height:.875rem" aria-hidden="true"></span>
                </button>
            </div>
            <div class="dm-win-messages">
                <div class="dm-win-loading">Loading…</div>
            </div>
            <div class="dm-win-typing" hidden><span></span><span></span><span></span></div>
            <div class="dm-win-compose">
                <textarea class="dm-win-textarea" placeholder="Aa" maxlength="2000" rows="1"></textarea>
                <button type="button" class="dm-win-send" disabled aria-label="Send">↑</button>
            </div>
        `;

        const closeBtn = win.querySelector<HTMLButtonElement>('.dm-win-close')!;
        const expandLink = win.querySelector<HTMLAnchorElement>('.dm-win-expand')!;
        const textarea = win.querySelector<HTMLTextAreaElement>('.dm-win-textarea')!;
        const sendBtn = win.querySelector<HTMLButtonElement>('.dm-win-send')!;

        closeBtn.addEventListener('click', () => closeWindow(conv.publicId));

        // Close the widget window when navigating to the full view
        expandLink.addEventListener('click', () => closeWindow(conv.publicId));

        let isTypingActive = false;
        let typingIdleTimer: ReturnType<typeof setTimeout> | null = null;

        function sendStartTyping(): void {
            if (!isTypingActive) {
                isTypingActive = true;
                (window as any).SnakkRealtime?.startDmTyping?.(conv.publicId, conv.otherUserPublicId);
            }
            if (typingIdleTimer) clearTimeout(typingIdleTimer);
            typingIdleTimer = setTimeout(sendStopTyping, 3000);
        }

        function sendStopTyping(): void {
            if (typingIdleTimer) { clearTimeout(typingIdleTimer); typingIdleTimer = null; }
            if (!isTypingActive) return;
            isTypingActive = false;
            (window as any).SnakkRealtime?.stopDmTyping?.(conv.publicId, conv.otherUserPublicId);
        }

        textarea.addEventListener('input', () => {
            sendBtn.disabled = !textarea.value.trim();
            // Auto-grow textarea (capped at ~4 lines)
            textarea.style.height = 'auto';
            textarea.style.height = Math.min(textarea.scrollHeight, 80) + 'px';
            sendStartTyping();
        });

        textarea.addEventListener('blur', sendStopTyping);

        textarea.addEventListener('keydown', e => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                if (!sendBtn.disabled) void sendMessage(conv.publicId);
            }
        });

        sendBtn.addEventListener('click', () => void sendMessage(conv.publicId));

        return win;
    }

    // ── Message loading ───────────────────────────────────────────────────────

    async function loadMessages(convId: string): Promise<void> {
        const state = openWindows.get(convId);
        if (!state) return;

        const messagesEl = state.el.querySelector<HTMLElement>('.dm-win-messages')!;
        messagesEl.innerHTML = '<div class="dm-win-loading">Loading…</div>';

        try {
            const res = await fetch(
                `/bff/messages/conversations/${encodeURIComponent(convId)}/messages?offset=0&pageSize=${MSG_PAGE_SIZE}`,
                { credentials: 'include' }
            );
            if (!res.ok) { messagesEl.innerHTML = '<div class="dm-win-loading">Could not load</div>'; return; }

            const data = await res.json() as { items: MessageInfo[] };
            messagesEl.innerHTML = '';

            // Items come newest-first — reverse for display
            const msgs = [...data.items].reverse();
            let prevSender = '';
            let prevDateStr = '';
            for (const m of msgs) {
                const dateStr = new Date(m.createdAt).toDateString();
                const needsSep = dateStr !== prevDateStr;
                const isFirst = needsSep || m.senderPublicId !== prevSender;
                appendBubble(messagesEl, m, isFirst, needsSep);
                prevSender = m.senderPublicId;
                prevDateStr = dateStr;
            }

            if (msgs.length > 0) {
                state.lastMessageId = msgs[msgs.length - 1]?.publicId ?? '';
            }

            scrollToBottom(messagesEl);

            void markAsReadAndUpdateBadge(convId);

        } catch {
            messagesEl.innerHTML = '<div class="dm-win-loading">Could not load</div>';
        }
    }

    async function checkForNewMessages(): Promise<void> {
        for (const [convId, state] of openWindows) {
            try {
                const res = await fetch(
                    `/bff/messages/conversations/${encodeURIComponent(convId)}/messages?offset=0&pageSize=5`,
                    { credentials: 'include' }
                );
                if (!res.ok) continue;

                const data = await res.json() as { items: MessageInfo[] };
                const messagesEl = state.el.querySelector<HTMLElement>('.dm-win-messages');
                if (!messagesEl) continue;

                // API returns newest-first. Collect unseen messages, then append oldest-first.
                const incoming: MessageInfo[] = [];
                for (const msg of data.items) {
                    if (msg.publicId === state.lastMessageId) break;
                    if (messagesEl.querySelector(`[data-msg-id="${msg.publicId}"]`)) break;
                    incoming.push(msg);
                }
                if (incoming.length > 0) {
                    const newestId = incoming[0]!.publicId;
                    const lastInfo = getLastBubbleInfo(messagesEl);
                    let prevSender = lastInfo?.senderPublicId ?? '';
                    let prevDateStr = lastInfo?.dateStr ?? '';

                    for (const msg of incoming.reverse()) {
                        const dateStr = new Date(msg.createdAt).toDateString();
                        const needsSep = dateStr !== prevDateStr;
                        const isFirst = needsSep || msg.senderPublicId !== prevSender;
                        appendBubble(messagesEl, msg, isFirst, needsSep);
                        prevSender = msg.senderPublicId;
                        prevDateStr = dateStr;
                    }
                    state.lastMessageId = newestId;

                    if (!document.hasFocus()) playMessageSound();

                    void markAsReadAndUpdateBadge(convId);
                }

                scrollToBottom(messagesEl);
            } catch { /* silent */ }
        }
    }

    // ── Send message ──────────────────────────────────────────────────────────

    async function sendMessage(convId: string): Promise<void> {
        const state = openWindows.get(convId);
        if (!state) return;

        const textarea = state.el.querySelector<HTMLTextAreaElement>('.dm-win-textarea')!;
        const sendBtn = state.el.querySelector<HTMLButtonElement>('.dm-win-send')!;
        const content = textarea.value.trim();
        if (!content) return;

        sendBtn.disabled = true;
        textarea.value = '';
        textarea.style.height = 'auto';
        (window as any).SnakkRealtime?.stopDmTyping?.(convId, state.conv.otherUserPublicId);

        try {
            const res = await fetch(`/bff/messages/conversations/${encodeURIComponent(convId)}/send`, {
                method: 'POST',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ content }),
            });
            if (!res.ok) { textarea.value = content; return; }

            const msg = await res.json() as MessageInfo;
            const messagesEl = state.el.querySelector<HTMLElement>('.dm-win-messages')!;
            const lastInfo = getLastBubbleInfo(messagesEl);
            const dateStr = new Date(msg.createdAt).toDateString();
            const needsSep = !lastInfo || dateStr !== lastInfo.dateStr;
            const isFirst = !lastInfo || needsSep || msg.senderPublicId !== lastInfo.senderPublicId;
            appendBubble(messagesEl, msg, isFirst, needsSep);
            state.lastMessageId = msg.publicId;
            scrollToBottom(messagesEl);

            // Notify full-view if open on the same conversation
            document.dispatchEvent(new CustomEvent('snakk:dm:message-sent', {
                detail: {
                    conversationId: convId,
                    publicId: msg.publicId,
                    content: msg.content,
                    createdAt: msg.createdAt,
                    senderPublicId: msg.senderPublicId,
                    isMine: msg.isMine,
                    source: 'widget'
                }
            }));

            // Refresh panel list (last message preview)
            panelLoaded = false;
        } catch {
            textarea.value = content;
        } finally {
            sendBtn.disabled = !textarea.value.trim();
        }
    }

    // ── Bubble rendering ──────────────────────────────────────────────────────

    function appendBubble(
        messagesEl: HTMLElement,
        msg: MessageInfo,
        isFirstInGroup = true,
        needsDateSep = false
    ): void {
        const existing = messagesEl.querySelector(`[data-msg-id="${msg.publicId}"]`);
        if (existing) return;

        if (needsDateSep) {
            const sep = document.createElement('div');
            sep.className = 'dm-date-separator';
            sep.innerHTML = `<span>${formatDate(msg.createdAt)}</span>`;
            messagesEl.appendChild(sep);
        }

        const bubble = document.createElement('div');
        bubble.className = `dm-bubble ${msg.isMine ? 'dm-bubble--mine' : 'dm-bubble--theirs'}${isFirstInGroup ? ' dm-bubble--first-in-group' : ''}`;
        bubble.dataset.msgId = msg.publicId;
        bubble.dataset.senderPublicId = msg.senderPublicId;
        bubble.dataset.dateStr = new Date(msg.createdAt).toDateString();
        bubble.innerHTML = `
            <div class="dm-bubble-body">
                <div class="dm-bubble-content">${escapeHtml(msg.content)}</div>
                <span class="dm-bubble-time">${formatTime(msg.createdAt)}</span>
            </div>
        `;
        messagesEl.appendChild(bubble);
    }

    function scrollToBottom(el: HTMLElement): void {
        el.scrollTop = el.scrollHeight;
    }

    // ── Realtime ─────────────────────────────────────────────────────────────

    const dmTypingTimeouts = new Map<string, ReturnType<typeof setTimeout>>();

    document.addEventListener('snakk:realtime:dm-typing', (e: Event) => {
        const { conversationId, isTyping } = (e as CustomEvent<{ conversationId: string; isTyping: boolean }>).detail;
        const state = openWindows.get(conversationId);
        if (!state) return;

        const typingEl = state.el.querySelector<HTMLElement>('.dm-win-typing');
        if (!typingEl) return;

        const existing = dmTypingTimeouts.get(conversationId);
        if (existing) clearTimeout(existing);

        if (isTyping) {
            typingEl.hidden = false;
            dmTypingTimeouts.set(conversationId, setTimeout(() => {
                typingEl.hidden = true;
                dmTypingTimeouts.delete(conversationId);
            }, 5000));
        } else {
            typingEl.hidden = true;
            dmTypingTimeouts.delete(conversationId);
        }
    });

    document.addEventListener('snakk:realtime:dm-count', () => {
        if (openWindows.size > 0) {
            void checkForNewMessages();
        } else {
            if (!document.hasFocus()) playMessageSound();
            if (!panelOpen) nudgeToggleBtn();
        }
        panelLoaded = false;
    });

    // Sync: message sent from full-view — show it in any matching open window
    document.addEventListener('snakk:dm:message-sent', (e: Event) => {
        const detail = (e as CustomEvent).detail as {
            conversationId: string; publicId: string; content: string;
            createdAt: string; senderPublicId: string; isMine: boolean; source: string;
        };
        if (detail.source === 'widget') return; // own event
        const state = openWindows.get(detail.conversationId);
        if (!state) return;
        const messagesEl = state.el.querySelector<HTMLElement>('.dm-win-messages');
        if (!messagesEl) return;
        if (messagesEl.querySelector(`[data-msg-id="${detail.publicId}"]`)) return;
        const msg: MessageInfo = {
            publicId: detail.publicId,
            content: detail.content,
            createdAt: detail.createdAt,
            isMine: detail.isMine,
            senderPublicId: detail.senderPublicId,
        };
        const lastInfo = getLastBubbleInfo(messagesEl);
        const dateStr = new Date(msg.createdAt).toDateString();
        const needsSep = !lastInfo || dateStr !== lastInfo.dateStr;
        const isFirst = !lastInfo || needsSep || msg.senderPublicId !== lastInfo.senderPublicId;
        appendBubble(messagesEl, msg, isFirst, needsSep);
        state.lastMessageId = msg.publicId;
        scrollToBottom(messagesEl);
    });

    // Realtime: messages hard-deleted by other user
    document.addEventListener('snakk:realtime:dm-messages-deleted', (e: Event) => {
        const detail = (e as CustomEvent).detail as { conversationId: string; messageIds: string[] };
        const state = openWindows.get(detail.conversationId);
        if (!state) return;
        const messagesEl = state.el.querySelector<HTMLElement>('.dm-win-messages');
        if (!messagesEl) return;
        if (detail.messageIds.length === 0) {
            // Entire conversation deleted — close the window
            closeWindow(detail.conversationId);
        } else {
            detail.messageIds.forEach(id => {
                messagesEl.querySelector(`[data-msg-id="${id}"]`)?.remove();
            });
        }
    });

    // ── Init ──────────────────────────────────────────────────────────────────

    function init(): void {
        createWidget();

        // Restore windows from previous HTMX navigation (hard reload restore)
        const saved = loadOpenState();
        for (const conv of saved) openWindow(conv);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    (window as any).DmWidget = {
        open: openWindow,
        openForRecipient: openWindowForRecipient,
    };
})();
