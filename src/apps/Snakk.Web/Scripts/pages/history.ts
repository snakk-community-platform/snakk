(function(): void {
    'use strict';

    const BATCH_SIZE = 20;
    const STORAGE_KEY = 'snakk_read_history';

    interface ReadHistoryEntry {
        discussionPublicId: string;
        visitedAt?: string;
    }

    interface HistoryItem {
        id: string;
        visitedAt?: string;
    }

    // Incremented each time init() starts; lets in-flight fetches from a
    // superseded init() detect they're stale and bail out cleanly.
    let generation = 0;

    function readEntries(): HistoryItem[] {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            if (!raw) return [];
            const entries = JSON.parse(raw) as ReadHistoryEntry[];
            return entries
                .filter(e => e.discussionPublicId)
                .map(e => ({ id: e.discussionPublicId, visitedAt: e.visitedAt }));
        } catch {
            return [];
        }
    }

    function chunk<T>(arr: T[], size: number): T[][] {
        const chunks: T[][] = [];
        for (let i = 0; i < arr.length; i += size) {
            chunks.push(arr.slice(i, i + size));
        }
        return chunks;
    }

    function removeEntry(publicId: string): void {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            if (!raw) return;
            const entries = JSON.parse(raw) as ReadHistoryEntry[];
            const filtered = entries.filter(e => e.discussionPublicId !== publicId);
            localStorage.setItem(STORAGE_KEY, JSON.stringify(filtered));
        } catch { }
    }

    function handleRemove(btn: HTMLElement): void {
        const publicId = btn.dataset['removeHistoryId'];
        if (!publicId) return;

        const card = btn.closest('.sn-card') as HTMLElement | null;
        card?.remove();

        removeEntry(publicId);

        const container = document.getElementById('history-container');
        const empty     = document.getElementById('history-empty');
        if (container && !container.querySelector('.sn-card')) {
            container.classList.add('hidden');
            empty?.classList.remove('hidden');
        }
    }

    function handleClear(): void {
        try { localStorage.removeItem(STORAGE_KEY); } catch { }

        const container = document.getElementById('history-container');
        const empty     = document.getElementById('history-empty');

        if (container) { container.innerHTML = ''; container.classList.add('hidden'); }
        empty?.classList.remove('hidden');
    }

    async function init(): Promise<void> {
        const myGen     = ++generation;
        const loadingEl = document.getElementById('history-loading');
        const container = document.getElementById('history-container');
        const empty     = document.getElementById('history-empty');

        if (!container) return; // not on the history page

        // Always reset before (re-)loading — prevents double-render on HTMX back-nav
        container.innerHTML = '';
        container.classList.add('hidden');
        empty?.classList.add('hidden');
        loadingEl?.classList.remove('hidden');

        const allEntries = readEntries();

        if (allEntries.length === 0) {
            loadingEl?.classList.add('hidden');
            empty?.classList.remove('hidden');
            return;
        }

        // Fire all batches concurrently; history is capped at 50 entries so
        // this is at most 3 parallel requests (batch size 20).
        const htmlParts = await Promise.all(
            chunk(allEntries, BATCH_SIZE).map(async (batch) => {
                try {
                    const ids = batch.map(e => e.id).join(',');
                    const tss = batch.map(e => e.visitedAt ?? '').join(',');
                    const resp = await fetch(`/partials/history-discussions?ids=${ids}&timestamps=${encodeURIComponent(tss)}`, {
                        credentials: 'include'
                    });
                    if (myGen !== generation) return '';
                    if (resp.ok) return await resp.text();
                } catch { }
                return '';
            })
        );

        if (myGen !== generation) return;

        loadingEl?.classList.add('hidden');

        const combinedHtml = htmlParts.join('');
        if (combinedHtml.trim()) {
            container.insertAdjacentHTML('beforeend', combinedHtml);
            // Let card-animations.js observe the newly inserted poll/debate/gallery cards
            document.dispatchEvent(new CustomEvent('htmx:afterSwap', { detail: { target: container } }));
            container.classList.remove('hidden');
        } else {
            empty?.classList.remove('hidden');
        }
    }

    function setup(): void {
        // Clone the button to remove stale listeners from any previous page load
        const clearBtn = document.getElementById('history-clear-btn');
        if (clearBtn) {
            const fresh = clearBtn.cloneNode(true) as HTMLElement;
            clearBtn.parentNode?.replaceChild(fresh, clearBtn);
            fresh.addEventListener('click', handleClear);
        }

        if (!(window as any).__historyRemoveListenerRegistered) {
            (window as any).__historyRemoveListenerRegistered = true;
            document.addEventListener('click', function(e: Event) {
                const btn = (e.target as HTMLElement).closest('[data-action="remove-from-history"]') as HTMLElement | null;
                if (btn) handleRemove(btn);
            });
        }

        void init();
    }

    // Initial run (F5 / direct navigation)
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', setup);
    } else {
        setup();
    }

    // HTMX soft-navigation: re-init when this page is swapped in.
    // Guard with a window flag so we register at most once even if the script
    // tag is re-evaluated by HTMX across multiple navigations.
    if (!(window as any).__historyHtmxListenerRegistered) {
        (window as any).__historyHtmxListenerRegistered = true;
        document.addEventListener('htmx:afterSettle', function(e) {
            const target = (e as CustomEvent).detail?.target;
            if (target === document.body && document.getElementById('history-container')) {
                setup();
            }
        });
    }
})();
