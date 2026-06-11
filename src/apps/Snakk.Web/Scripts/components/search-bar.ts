/**
 * Inline search bar.
 * Mobile: focus opens a unified filter popup below the input.
 * Desktop: focus reveals filter pills; each pill opens its own independent dropdown.
 */

(function (): void {
    'use strict';

    if ((window as any).SnakkSearch) return;

    const wrap      = document.getElementById('sn-search-wrap');
    const input     = document.getElementById('sn-search-input') as HTMLInputElement | null;
    const popup     = document.getElementById('sn-search-popup') as HTMLElement | null;
    const dateRange = document.getElementById('sn-date-range')   as HTMLInputElement | null;  // mobile
    const dateRangeD = document.getElementById('sn-date-range-d') as HTMLInputElement | null; // desktop

    if (!wrap || !input || !popup) return;

    const DATE_VALUES = ['past hour', 'today', 'past week', 'past month', 'past year', 'all time'] as const;
    const PILL_KEYS = ['type', 'sort', 'date'] as const;
    type PillKey = typeof PILL_KEYS[number];

    // ── Helpers ───────────────────────────────────────────────────────────────

    function isDesktop(): boolean { return window.innerWidth >= 1024; }

    function updatePillLabels(): void {
        const desk = isDesktop();
        const typeVal = document.querySelector<HTMLInputElement>(`input[name="${desk ? 'sn-search-type-d' : 'sn-search-type'}"]:checked`)?.value ?? 'discussion';
        const sortVal = document.querySelector<HTMLInputElement>(`input[name="${desk ? 'sn-search-sort-d' : 'sn-search-sort'}"]:checked`)?.value ?? 'newest';
        const dateEl  = desk ? dateRangeD : dateRange;
        const dateIdx = parseInt(dateEl?.value ?? '2', 10);

        const tl = document.getElementById('sn-pill-type-label');
        const sl = document.getElementById('sn-pill-sort-label');
        const dl = document.getElementById('sn-pill-date-label');

        // Read label text from the mobile popup (same strings as desktop dropdown)
        if (tl) tl.textContent = document.querySelector<HTMLLabelElement>(`label[for="sn-type-${typeVal}"]`)?.textContent?.trim() ?? typeVal;
        if (sl) sl.textContent = document.querySelector<HTMLLabelElement>(`label[for="sn-sort-${sortVal}"]`)?.textContent?.trim() ?? sortVal;
        if (dl) dl.textContent = popup!.querySelectorAll<HTMLElement>('.sn-search-date-ticks span')[dateIdx]?.textContent?.trim() ?? '';
    }

    // ── Desktop dropdowns ─────────────────────────────────────────────────────

    function openDropdown(key: PillKey): void {
        const pill     = document.getElementById(`sn-pill-${key}`);
        const dropdown = document.getElementById(`sn-dropdown-${key}`);
        if (!pill || !dropdown) return;

        // Toggle off if already open
        if (!dropdown.hidden && dropdown.classList.contains('sn-filter-dropdown--open')) {
            closeDropdown(key);
            return;
        }

        // Close any other open dropdown
        PILL_KEYS.forEach(k => { if (k !== key) closeDropdown(k); });

        PILL_KEYS.forEach(k => document.getElementById(`sn-pill-${k}`)?.classList.remove('sn-filter-pill--active'));
        pill.classList.add('sn-filter-pill--active');

        dropdown.hidden = false;
        requestAnimationFrame(() => dropdown.classList.add('sn-filter-dropdown--open'));
    }

    function closeDropdown(key: PillKey): void {
        document.getElementById(`sn-pill-${key}`)?.classList.remove('sn-filter-pill--active');
        const dropdown = document.getElementById(`sn-dropdown-${key}`);
        if (!dropdown) return;
        dropdown.classList.remove('sn-filter-dropdown--open');
        dropdown.hidden = true;
    }

    function closeAllDropdowns(): void {
        PILL_KEYS.forEach(k => closeDropdown(k));
    }

    // ── Mobile popup ──────────────────────────────────────────────────────────

    function openMobilePopup(): void {
        wrap!.classList.add('sn-search-focused');
        popup!.hidden = false;
        requestAnimationFrame(() => popup!.classList.add('sn-search-popup--open'));
    }

    // ── Full close (blur / Escape) ────────────────────────────────────────────

    function closeAll(): void {
        closeAllDropdowns();
        popup!.classList.remove('sn-search-popup--open');
        popup!.hidden = true;
        wrap!.classList.remove('sn-search-focused');
    }

    // ── Page sync ─────────────────────────────────────────────────────────────

    function isSearchPage(): boolean {
        return window.location.pathname.toLowerCase() === '/search';
    }

    function syncToPage(): void {
        if (isSearchPage()) {
            input!.value = new URLSearchParams(window.location.search).get('q') ?? '';
            if (!isDesktop()) openMobilePopup();
            else wrap!.classList.add('sn-search-focused');
            input!.focus({ preventScroll: true });
        } else {
            input!.value = '';
            closeAll();
        }
    }

    // ── Submit ────────────────────────────────────────────────────────────────

    function submitSearch(): void {
        const q = input!.value.trim();
        if (!q) return;
        // Don't close — syncToPage() will set correct state once navigation settles.
        const desk = isDesktop();
        const type    = document.querySelector<HTMLInputElement>(`input[name="${desk ? 'sn-search-type-d' : 'sn-search-type'}"]:checked`)?.value ?? '';
        const dateEl  = desk ? dateRangeD : dateRange;
        const dateIdx = parseInt(dateEl?.value ?? '2', 10);
        const date    = DATE_VALUES[dateIdx] ?? 'all time';
        const sort    = document.querySelector<HTMLInputElement>(`input[name="${desk ? 'sn-search-sort-d' : 'sn-search-sort'}"]:checked`)?.value ?? '';
        let url = `/search?q=${encodeURIComponent(q)}`;
        if (type)                    url += `&searchType=${encodeURIComponent(type)}`;
        if (date !== 'all time')     url += `&dateRange=${encodeURIComponent(date)}`;
        if (sort && sort !== 'newest') url += `&sortBy=${encodeURIComponent(sort)}`;
        const htmx = (window as any).htmx;
        if (htmx?.ajax) {
            htmx.ajax('GET', url, { target: '#main-content', swap: 'outerHTML show:window:top', headers: { 'HX-Boosted': 'true' } });
        } else {
            window.location.href = url;
        }
    }

    // ── Event wiring ──────────────────────────────────────────────────────────

    input.addEventListener('focus', () => {
        if (!isDesktop()) openMobilePopup();
        else wrap!.classList.add('sn-search-focused');
    });

    input.addEventListener('blur', () => {
        setTimeout(() => {
            if (!wrap!.contains(document.activeElement) && !isSearchPage()) closeAll();
        }, 150);
    });

    input.addEventListener('keydown', (e: KeyboardEvent) => {
        if (e.key === 'Escape') { closeAll(); input!.blur(); }
        if (e.key === 'Enter')  { e.preventDefault(); submitSearch(); }
    });

    // Mobile popup changes: sync pills
    popup.addEventListener('change', () => {
        updatePillLabels();
    });
    dateRange?.addEventListener('input', updatePillLabels);

    // Prevent mobile popup clicks from blurring input; allow slider drag
    popup.addEventListener('mousedown', (e: MouseEvent) => {
        if ((e.target as HTMLInputElement).type !== 'range') e.preventDefault();
    });

    // Desktop dropdown changes: update pill labels, auto-close on radio select, refocus input
    const pillsContainer = document.querySelector<HTMLElement>('.sn-filter-pills');
    pillsContainer?.addEventListener('change', (e: Event) => {
        const target = e.target as HTMLInputElement;
        updatePillLabels();
        if (target.type === 'radio') {
            const dropdownEl = target.closest<HTMLElement>('.sn-filter-dropdown');
            if (dropdownEl) closeDropdown(dropdownEl.id.replace('sn-dropdown-', '') as PillKey);
            input!.focus();
        }
    });
    dateRangeD?.addEventListener('input', updatePillLabels);
    dateRangeD?.addEventListener('change', () => { closeDropdown('date'); input!.focus(); });

    // Prevent desktop dropdown clicks from blurring input; allow slider drag
    pillsContainer?.addEventListener('mousedown', (e: MouseEvent) => {
        if ((e.target as HTMLInputElement).type !== 'range') e.preventDefault();
    });

    // Pill buttons: open their dropdown
    PILL_KEYS.forEach(key => {
        document.getElementById(`sn-pill-${key}`)?.addEventListener('mousedown', (e: MouseEvent) => {
            e.preventDefault();
            if (!wrap!.classList.contains('sn-search-focused')) wrap!.classList.add('sn-search-focused');
            openDropdown(key);
        });
    });

    document.getElementById('sn-search-submit')?.addEventListener('click', submitSearch);

    // Expand + focus on search page; collapse + clear on every other page.
    // Runs on initial load, after every full-page HTMX swap, and after history restore.
    syncToPage();
    document.addEventListener('htmx:afterSettle', (e: Event) => {
        if (((e as CustomEvent).detail?.target as Element)?.id === 'main-content') syncToPage();
    });
    document.addEventListener('htmx:historyRestore', syncToPage);

    (window as any).SnakkSearch = { open: openMobilePopup, close: closeAll };
})();
