/**
 * Inline search bar — replaces the old search-modal dialog.
 * Focus expands the bar and shows a filter popup; Enter navigates to /search.
 */

(function (): void {
    'use strict';

    if ((window as any).SnakkSearch) return;

    const wrap = document.getElementById('sn-search-wrap');
    const input = document.getElementById('sn-search-input') as HTMLInputElement | null;
    const popup = document.getElementById('sn-search-popup') as HTMLElement | null;

    if (!wrap || !input || !popup) return;

    function openPopup(): void {
        popup!.hidden = false;
        wrap!.classList.add('sn-search-focused');
    }

    function closePopup(): void {
        popup!.hidden = true;
        wrap!.classList.remove('sn-search-focused');
    }

    function submitSearch(): void {
        const q = input!.value.trim();
        if (!q) return;
        closePopup();
        const type = document.querySelector<HTMLInputElement>('input[name="sn-search-type"]:checked')?.value ?? '';
        const date = document.querySelector<HTMLInputElement>('input[name="sn-search-date"]:checked')?.value ?? '';
        let url = `/search?q=${encodeURIComponent(q)}`;
        if (type) url += `&searchType=${encodeURIComponent(type)}`;
        if (date && date !== 'all time') url += `&dateRange=${encodeURIComponent(date)}`;
        const htmx = (window as any).htmx;
        if (htmx?.ajax) {
            htmx.ajax('GET', url, {
                target: '#main-content',
                swap: 'outerHTML show:window:top',
                headers: { 'HX-Boosted': 'true' },
            });
        } else {
            window.location.href = url;
        }
    }

    input.addEventListener('focus', openPopup);

    input.addEventListener('blur', () => {
        // Delay so clicks inside the popup register before hiding it
        setTimeout(() => {
            if (!wrap!.contains(document.activeElement)) closePopup();
        }, 150);
    });

    input.addEventListener('keydown', (e: KeyboardEvent) => {
        if (e.key === 'Escape') { closePopup(); input!.blur(); }
        if (e.key === 'Enter') { e.preventDefault(); submitSearch(); }
    });

    // Prevent radio clicks from stealing focus away from the input
    popup.addEventListener('mousedown', (e: MouseEvent) => e.preventDefault());

    (window as any).SnakkSearch = { open: openPopup, close: closePopup };
})();
