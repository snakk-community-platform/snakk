/**
 * DM Detail Page
 * Handles: plaintext compose, send, delete messages, delete conversation, realtime sync.
 */

(function(): void {
    'use strict';

    const T = (window as any).T;

    const conversationId = (document.getElementById('dm-conversation-id') as HTMLInputElement | null)?.value ?? '';
    const currentUserPublicId = (document.getElementById('dm-current-user-id') as HTMLInputElement | null)?.value ?? '';
    let lastKnownMessagePublicId = '';
    let isSending = false;

    const textarea = document.getElementById('dm-compose-textarea') as HTMLTextAreaElement | null;
    const sendBtn = document.getElementById('dm-send-btn') as HTMLButtonElement | null;
    const deletePaneEl = document.getElementById('dm-delete-pane') as HTMLElement | null;
    const selectedCountEl = document.getElementById('dm-selected-count') as HTMLElement | null;
    const deleteSelectedBtn = document.getElementById('dm-delete-selected-btn') as HTMLButtonElement | null;
    const deleteConvBtn = document.getElementById('dm-delete-conv-btn') as HTMLButtonElement | null;
    const deleteModal = document.getElementById('dm-delete-modal') as HTMLDialogElement | null;
    const deleteForMeBtn = document.getElementById('dm-delete-for-me-btn') as HTMLButtonElement | null;
    const deleteForAllBtn = document.getElementById('dm-delete-for-all-btn') as HTMLButtonElement | null;
    const deleteModalTitle = deleteModal?.querySelector<HTMLElement>('#dm-delete-modal-title');
    const deleteModalDesc = deleteModal?.querySelector<HTMLElement>('#dm-delete-modal-desc');

    let deleteModalMode: 'messages' | 'conversation' = 'messages';

    // ─── Compose textarea auto-resize ─────────────────────────────

    function resizeTextarea(): void {
        if (!textarea) return;
        textarea.style.height = 'auto';
        // cap at ~5 lines (100px)
        textarea.style.height = Math.min(textarea.scrollHeight, 100) + 'px';
    }

    function updateSendBtn(): void {
        if (sendBtn) sendBtn.disabled = !(textarea?.value.trim()) || isSending;
    }

    textarea?.addEventListener('input', () => {
        updateSendBtn();
        resizeTextarea();
    });

    textarea?.addEventListener('keydown', (e: KeyboardEvent) => {
        if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
            e.preventDefault();
            void sendMessage();
        }
    });

    sendBtn?.addEventListener('click', () => void sendMessage());

    // ─── Send Message ─────────────────────────────────────────────

    async function sendMessage(): Promise<void> {
        const content = textarea?.value.trim() ?? '';
        if (!content || isSending || !conversationId) return;

        isSending = true;
        updateSendBtn();

        try {
            const response = await fetch(`/bff/messages/conversations/${encodeURIComponent(conversationId)}/send`, {
                method: 'POST',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ content })
            });

            if (!response.ok) {
                console.error('[DM] sendMessage failed:', response.status);
                return;
            }

            const message = await response.json() as {
                publicId: string;
                content: string;
                createdAt: string;
                isMine: boolean;
            };

            appendMessageBubble(message.publicId, message.content, message.createdAt, true, currentUserPublicId);
            lastKnownMessagePublicId = message.publicId;

            if (textarea) { textarea.value = ''; resizeTextarea(); }
            updateSendBtn();
            scrollToBottom();

            document.dispatchEvent(new CustomEvent('snakk:dm:message-sent', {
                detail: {
                    conversationId,
                    publicId: message.publicId,
                    content: message.content,
                    createdAt: message.createdAt,
                    senderPublicId: currentUserPublicId,
                    isMine: true,
                    source: 'detail'
                }
            }));
        } catch (err) {
            console.warn('[DM] Failed to send message:', err);
        } finally {
            isSending = false;
            updateSendBtn();
        }
    }

    // ─── Render helpers ───────────────────────────────────────────

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
                a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
            if (sameDay(date, now)) return 'Today';
            const yesterday = new Date(now);
            yesterday.setDate(now.getDate() - 1);
            if (sameDay(date, yesterday)) return 'Yesterday';
            const opts: Intl.DateTimeFormatOptions = { month: 'long', day: 'numeric' };
            if (date.getFullYear() !== now.getFullYear()) opts.year = 'numeric';
            return date.toLocaleDateString([], opts);
        } catch { return ''; }
    }

    function scrollToBottom(): void {
        const list = document.getElementById('dm-message-list');
        if (list) list.scrollTop = list.scrollHeight;
    }

    function getLastBubbleInfo(): { senderPublicId: string; dateStr: string } | null {
        const list = document.getElementById('dm-message-list');
        if (!list) return null;
        const bubbles = list.querySelectorAll<HTMLElement>('.sn-dm-bubble');
        const last = bubbles[bubbles.length - 1];
        if (!last) return null;
        return { senderPublicId: last.dataset.senderPublicId ?? '', dateStr: last.dataset.dateStr ?? '' };
    }

    function appendMessageBubble(
        publicId: string, content: string, createdAt: string, isMine: boolean, senderPublicId: string
    ): void {
        const list = document.getElementById('dm-message-list');
        if (!list) return;
        if (document.getElementById(`dm-msg-${publicId}`)) return;

        const dateStr = new Date(createdAt).toISOString().slice(0, 10);
        const lastInfo = getLastBubbleInfo();
        const needsDateSep = !lastInfo || lastInfo.dateStr !== dateStr;
        const isFirstInGroup = needsDateSep || !lastInfo || lastInfo.senderPublicId !== senderPublicId;

        if (needsDateSep) {
            const sep = document.createElement('div');
            sep.className = 'sn-dm-date-separator';
            sep.innerHTML = `<span>${formatDate(createdAt)}</span>`;
            list.appendChild(sep);
        }

        const wrapper = document.createElement('div');
        wrapper.id = `dm-msg-${publicId}`;
        wrapper.className = 'sn-dm-msg-row sn-flex sn-items-center gap-1.5';
        wrapper.dataset.msgId = publicId;
        wrapper.dataset.isMine = String(isMine);

        const bubbleClass = ['sn-dm-bubble', isMine ? 'dm-bubble--mine' : 'dm-bubble--theirs',
            isFirstInGroup ? 'sn-dm-bubble--first-in-group' : '', 'sn-flex-1'].filter(Boolean).join(' ');

        wrapper.innerHTML = `
            <input type="checkbox" class="dm-msg-check sn-dm-detail-check" aria-label="Select message">
            <div class="${bubbleClass}"
                 data-sender-public-id="${escapeHtml(senderPublicId)}"
                 data-date-str="${escapeHtml(dateStr)}">
                <div class="sn-dm-bubble-body">
                    <div class="sn-dm-bubble-content">${escapeHtml(content)}</div>
                    <span class="sn-dm-bubble-time">${formatTime(createdAt)}</span>
                </div>
            </div>`;

        attachCheckboxListener(wrapper);
        list.appendChild(wrapper);
    }

    // ─── Selection & delete pane ──────────────────────────────────

    function attachCheckboxListener(row: HTMLElement): void {
        row.querySelector<HTMLInputElement>('.dm-msg-check')?.addEventListener('change', updateDeletePane);
    }

    function getCheckedMessageIds(): string[] {
        const ids: string[] = [];
        document.querySelectorAll<HTMLElement>('.sn-dm-msg-row').forEach(row => {
            const cb = row.querySelector<HTMLInputElement>('.dm-msg-check');
            if (cb?.checked) {
                const id = row.dataset.msgId ?? row.id.replace('dm-msg-', '');
                if (id) ids.push(id);
            }
        });
        return ids;
    }

    function clearAllCheckboxes(): void {
        document.querySelectorAll<HTMLInputElement>('.dm-msg-check').forEach(cb => { cb.checked = false; });
        updateDeletePane();
    }

    function updateDeletePane(): void {
        const ids = getCheckedMessageIds();
        if (!deletePaneEl) return;
        if (ids.length === 0) {
            deletePaneEl.classList.add('sn-hidden');
        } else {
            deletePaneEl.classList.remove('sn-hidden');
            if (selectedCountEl) selectedCountEl.textContent = String(ids.length);
        }
    }

    // ─── Delete Modal ─────────────────────────────────────────────

    deleteSelectedBtn?.addEventListener('click', () => {
        const ids = getCheckedMessageIds();
        if (ids.length === 0) return;
        deleteModalMode = 'messages';
        if (deleteModalTitle) deleteModalTitle.textContent = (T?.pages?.messagesDeleteMsgsTitle ?? 'Delete {0} message(s)').replace('{0}', String(ids.length));
        if (deleteModalDesc) deleteModalDesc.textContent = T?.pages?.messagesDeleteMsgsDesc ?? 'Choose how to delete the selected messages.';
        if (deleteForMeBtn) deleteForMeBtn.textContent = T?.pages?.messagesDeleteForMe ?? 'Delete for me';
        if (deleteForAllBtn) deleteForAllBtn.textContent = T?.pages?.messagesDeleteForAll ?? 'Delete for everyone';
        deleteModal?.showModal();
    });

    deleteConvBtn?.addEventListener('click', () => {
        deleteModalMode = 'conversation';
        if (deleteModalTitle) deleteModalTitle.textContent = T?.pages?.messagesDeleteTitle ?? 'Delete conversation';
        if (deleteModalDesc) deleteModalDesc.textContent = T?.pages?.messagesDeleteDesc ?? 'Choose how to delete this conversation.';
        if (deleteForMeBtn) deleteForMeBtn.textContent = T?.pages?.messagesDeleteForMe ?? 'Delete for me';
        if (deleteForAllBtn) deleteForAllBtn.textContent = T?.pages?.messagesDeleteForAll ?? 'Delete for everyone';
        deleteModal?.showModal();
    });

    deleteForMeBtn?.addEventListener('click', () => {
        deleteModal?.close();
        if (deleteModalMode === 'messages') void deleteMessages(false);
        else void deleteConversation(false);
    });

    deleteForAllBtn?.addEventListener('click', () => {
        deleteModal?.close();
        if (deleteModalMode === 'messages') void deleteMessages(true);
        else void deleteConversation(true);
    });

    // ─── Delete Messages ──────────────────────────────────────────

    async function deleteMessages(deleteForAll: boolean): Promise<void> {
        const ids = getCheckedMessageIds();
        if (ids.length === 0 || !conversationId) return;
        try {
            const response = await fetch(
                `/bff/messages/conversations/${encodeURIComponent(conversationId)}/messages`,
                {
                    method: 'DELETE',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ messageIds: ids, deleteForAll })
                }
            );
            if (!response.ok) {
                console.error('[DM] deleteMessages failed:', response.status);
                return;
            }
            removeMessageElements(ids);
            clearAllCheckboxes();
        } catch (err) {
            console.warn('[DM] Failed to delete messages:', err);
        }
    }

    function removeMessageElements(ids: string[]): void {
        for (const id of ids) {
            (document.getElementById(`dm-msg-${id}`) ??
             document.querySelector<HTMLElement>(`[data-msg-id="${id}"]`))?.remove();
        }
    }

    // ─── Delete Conversation ──────────────────────────────────────

    async function deleteConversation(deleteForAll: boolean): Promise<void> {
        if (!conversationId) return;
        try {
            const response = await fetch(
                `/bff/messages/conversations/${encodeURIComponent(conversationId)}?deleteForAll=${deleteForAll}`,
                { method: 'DELETE', credentials: 'include' }
            );
            if (response.ok) window.location.href = '/messages';
            else console.error('[DM] deleteConversation failed:', response.status);
        } catch (err) {
            console.warn('[DM] Failed to delete conversation:', err);
        }
    }

    // ─── Load New Messages (realtime incoming) ────────────────────

    async function checkForNewMessages(): Promise<void> {
        if (!conversationId) return;
        try {
            const response = await fetch(
                `/bff/messages/conversations/${encodeURIComponent(conversationId)}/messages?offset=0&pageSize=5`,
                { credentials: 'include' }
            );
            if (!response.ok) return;
            const data = await response.json() as { items: Array<{ publicId: string; content: string; createdAt: string; senderPublicId: string }> };
            const incoming: Array<{ publicId: string; content: string; createdAt: string; senderPublicId: string }> = [];
            for (const msg of data.items) {
                if (msg.publicId === lastKnownMessagePublicId) break;
                if (document.getElementById(`dm-msg-${msg.publicId}`)) break;
                incoming.push(msg);
            }
            if (incoming.length > 0) {
                const newestId = incoming[0]!.publicId;
                for (const msg of incoming.reverse()) {
                    appendMessageBubble(msg.publicId, msg.content, msg.createdAt, msg.senderPublicId === currentUserPublicId, msg.senderPublicId);
                }
                lastKnownMessagePublicId = newestId;
                scrollToBottom();
                fetch(`/bff/messages/conversations/${encodeURIComponent(conversationId)}/read`, {
                    method: 'POST', credentials: 'include'
                }).catch(() => undefined);
            }
        } catch (err) {
            console.warn('[DM] Failed to check for new messages:', err);
        }
    }

    // ─── Events ───────────────────────────────────────────────────

    document.addEventListener('snakk:realtime:dm-count', () => {
        if (document.getElementById('dm-message-list')) void checkForNewMessages();
    });

    document.addEventListener('snakk:dm:message-sent', (e: Event) => {
        const detail = (e as CustomEvent).detail as {
            conversationId: string; publicId: string; content: string;
            createdAt: string; senderPublicId: string; isMine: boolean; source: string;
        };
        if (detail.source === 'detail') return;
        if (detail.conversationId !== conversationId) return;
        appendMessageBubble(detail.publicId, detail.content, detail.createdAt, detail.isMine, detail.senderPublicId);
        scrollToBottom();
    });

    document.addEventListener('snakk:realtime:dm-messages-deleted', (e: Event) => {
        const detail = (e as CustomEvent).detail as { conversationId: string; messageIds: string[] };
        if (detail.conversationId !== conversationId) return;
        if (detail.messageIds.length === 0) window.location.href = '/messages';
        else removeMessageElements(detail.messageIds);
    });

    // ─── Init ─────────────────────────────────────────────────────

    document.querySelectorAll<HTMLElement>('.sn-dm-msg-row').forEach(attachCheckboxListener);

    function init(): void {
        const messages = document.querySelectorAll('[id^="dm-msg-"]');
        const last = messages[messages.length - 1];
        if (last) lastKnownMessagePublicId = last.id.replace('dm-msg-', '');
        scrollToBottom();
        resizeTextarea();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
