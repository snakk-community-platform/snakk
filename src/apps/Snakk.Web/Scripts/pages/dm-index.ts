/**
 * DM Index Page
 * Handles: pin/unpin conversation, delete conversation.
 */

(function(): void {
    'use strict';

    let pendingDeleteConvId = '';
    const deleteModal = document.getElementById('dm-delete-conv-modal') as HTMLDialogElement | null;
    const confirmDeleteForMeBtn = document.getElementById('dm-confirm-delete-for-me-btn') as HTMLButtonElement | null;
    const confirmDeleteForAllBtn = document.getElementById('dm-confirm-delete-for-all-btn') as HTMLButtonElement | null;

    // ─── Event delegation on conversation list ─────────────────────

    document.getElementById('dm-conv-list')?.addEventListener('click', (e: Event) => {
        const pinBtn = (e.target as Element).closest<HTMLButtonElement>('.dm-pin-btn');
        if (pinBtn) {
            e.preventDefault();
            e.stopPropagation();
            void togglePin(pinBtn);
            return;
        }

        const deleteBtn = (e.target as Element).closest<HTMLButtonElement>('.dm-delete-conv-btn');
        if (deleteBtn) {
            e.preventDefault();
            e.stopPropagation();
            const convId = deleteBtn.dataset.convId ?? '';
            if (!convId) return;
            pendingDeleteConvId = convId;
            deleteModal?.showModal();
        }
    });

    // ─── Pin ──────────────────────────────────────────────────────

    async function togglePin(btn: HTMLButtonElement): Promise<void> {
        const convId = btn.dataset.convId ?? '';
        if (!convId) return;

        const currentlyPinned = btn.dataset.isPinned === 'true';
        const newPinned = !currentlyPinned;

        try {
            const response = await fetch(
                `/bff/messages/conversations/${encodeURIComponent(convId)}/pin`,
                {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ isPinned: newPinned })
                }
            );
            if (!response.ok) {
                console.warn('[DM] Pin failed with status', response.status);
                return;
            }

            // Update button and row state
            btn.dataset.isPinned = String(newPinned);
            btn.title = newPinned ? 'Unpin' : 'Pin';
            btn.setAttribute('aria-label', newPinned ? 'Unpin conversation' : 'Pin conversation');

            const icon = btn.querySelector('.sn-icon');
            if (icon) {
                icon.className = `sn-icon ${newPinned ? 'icon-bookmark' : 'icon-bookmark-alt'} sn-h-3.5 sn-w-3.5`;
            }

            const row = btn.closest<HTMLElement>('.dm-conv-row');
            if (row) row.dataset.isPinned = String(newPinned);

            // Update pin indicator in conversation row
            const pinIndicator = row?.querySelector<HTMLElement>('.dm-pin-indicator');
            if (pinIndicator) pinIndicator.style.display = newPinned ? '' : 'none';

            reorderConversations();
        } catch (err) {
            console.warn('[DM] Failed to pin conversation:', err);
        }
    }

    function reorderConversations(): void {
        const list = document.getElementById('dm-conv-list');
        if (!list) return;

        const rows = Array.from(list.querySelectorAll<HTMLElement>('.dm-conv-row'));
        const pinned = rows.filter(r => r.dataset.isPinned === 'true');
        const unpinned = rows.filter(r => r.dataset.isPinned !== 'true');

        for (const row of [...pinned, ...unpinned]) {
            list.appendChild(row);
        }
    }

    // ─── Delete Conversation ──────────────────────────────────────

    confirmDeleteForMeBtn?.addEventListener('click', () => {
        if (!pendingDeleteConvId) return;
        deleteModal?.close();
        void deleteConversation(pendingDeleteConvId, false);
        pendingDeleteConvId = '';
    });

    confirmDeleteForAllBtn?.addEventListener('click', () => {
        if (!pendingDeleteConvId) return;
        deleteModal?.close();
        void deleteConversation(pendingDeleteConvId, true);
        pendingDeleteConvId = '';
    });

    async function deleteConversation(convId: string, deleteForAll: boolean): Promise<void> {
        try {
            const response = await fetch(
                `/bff/messages/conversations/${encodeURIComponent(convId)}?deleteForAll=${deleteForAll}`,
                { method: 'DELETE', credentials: 'include' }
            );
            if (!response.ok) {
                console.warn('[DM] Delete conversation failed with status', response.status);
                return;
            }

            document.querySelector<HTMLElement>(`.dm-conv-row[data-conv-id="${convId}"]`)?.remove();
        } catch (err) {
            console.warn('[DM] Failed to delete conversation:', err);
        }
    }
})();
