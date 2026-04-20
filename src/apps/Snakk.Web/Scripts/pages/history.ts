(function(): void {
    'use strict';

    const PAGE_SIZE = 10;
    const STORAGE_KEY = 'snakk_read_history';

    interface ReadHistoryEntry {
        discussionPublicId: string;
    }

    interface PageState {
        allIds: string[];
        offset: number;
        fetching: boolean;
        scrollObserver: IntersectionObserver | null;
    }

    // Incremented each time init() starts; lets in-flight fetchBatch calls from
    // a superseded init() detect they're stale and bail out cleanly.
    let generation = 0;

    function readIds(): string[] {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            if (!raw) return [];
            const entries = JSON.parse(raw) as ReadHistoryEntry[];
            return entries.map(e => e.discussionPublicId).filter(Boolean);
        } catch {
            return [];
        }
    }

    async function fetchBatch(state: PageState, gen: number): Promise<void> {
        if (state.fetching) return;
        if (gen !== generation) return; // superseded by a newer init()
        const batch = state.allIds.slice(state.offset, state.offset + PAGE_SIZE);
        if (batch.length === 0) return;

        state.fetching = true;
        const container = document.getElementById('history-container');
        const sentinel  = document.getElementById('history-sentinel');

        try {
            const resp = await fetch(`/partials/history-discussions?ids=${batch.join(',')}`, {
                credentials: 'include'
            });
            if (gen !== generation) return; // superseded while awaiting fetch
            if (resp.ok && container) {
                const html = await resp.text();
                if (gen !== generation) return; // superseded while awaiting resp.text()
                if (html.trim()) {
                    container.insertAdjacentHTML('beforeend', html);
                    // Let card-animations.js observe the newly inserted poll/debate/gallery cards
                    document.dispatchEvent(new Event('htmx:afterSwap'));
                }
                state.offset += batch.length;
            }
        } catch {
            // silent fail — sentinel stays visible so user can retry by scrolling
        } finally {
            state.fetching = false;
        }

        if (gen !== generation) return;
        if (state.offset >= state.allIds.length) {
            sentinel?.classList.add('hidden');
            state.scrollObserver?.disconnect();
        }
    }

    function handleClear(): void {
        try { localStorage.removeItem(STORAGE_KEY); } catch { }

        const container = document.getElementById('history-container');
        const sentinel  = document.getElementById('history-sentinel');
        const empty     = document.getElementById('history-empty');

        if (container) { container.innerHTML = ''; container.classList.add('hidden'); }
        sentinel?.classList.add('hidden');
        empty?.classList.remove('hidden');
    }

    async function init(): Promise<void> {
        const myGen     = ++generation;
        const loadingEl = document.getElementById('history-loading');
        const container = document.getElementById('history-container');
        const empty     = document.getElementById('history-empty');
        const sentinel  = document.getElementById('history-sentinel');

        if (!container) return; // not on the history page

        // Always reset before (re-)loading — prevents double-render on HTMX back-nav
        container.innerHTML = '';
        container.classList.add('hidden');
        sentinel?.classList.add('hidden');
        empty?.classList.add('hidden');
        loadingEl?.classList.remove('hidden');

        const state: PageState = {
            allIds: readIds(),
            offset: 0,
            fetching: false,
            scrollObserver: null
        };

        if (state.allIds.length === 0) {
            loadingEl?.classList.add('hidden');
            empty?.classList.remove('hidden');
            return;
        }

        container.classList.remove('hidden');
        await fetchBatch(state, myGen);
        if (myGen !== generation) return;
        loadingEl?.classList.add('hidden');

        if (state.offset < state.allIds.length && sentinel) {
            sentinel.classList.remove('hidden');
            state.scrollObserver = new IntersectionObserver(entries => {
                if (entries[0]?.isIntersecting) void fetchBatch(state, myGen);
            }, { rootMargin: '200px' });
            state.scrollObserver.observe(sentinel);
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
    // The generation counter inside init() prevents double-fetching in the case
    // where both the IIFE and this listener fire for the same navigation.
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
